using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using OpusDotNet;
using System.Runtime.CompilerServices;
using TMPro;
using Virial;
using Virial.Assignable;
using Virial.Game;
using Virial.Media;
using static Nebula.Modules.MetaWidgetOld;

namespace Nebula.VoiceChat;

public interface IVoiceComponent
{
    //可聴範囲 (リスナーとして)
    public float Radious { get; }

    //音声に掛ける倍率 (スピーカーとして)
    public float Volume { get; }

    public Vector2 Position { get; }

    //指定のマイクからの音声を再生できるかどうか falseの場合再生できない
    public bool CanPlaySoundFrom(IVoiceComponent mic);

    public float CanCatch(Vector2 speaker)
    {
        float dis = speaker.Distance(Position);
        if (dis < Radious) return dis / Radious;
        return 0f;
    }
}

public enum VoiceType
{
    Normal,
    Ghost,
    Radio
}


public class VoiceChatRadio
{
    private Predicate<GamePlayer> predicate;
    public string DisplayRadioName { get; private set; }
    public Color Color { get; private set; }
    public int RadioMask
    {
        get
        {
            int mask = 0;
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo) if (predicate.Invoke(p)) mask |= 1 << p.PlayerId;
            return mask;
        } 
    }

    public VoiceChatRadio(Predicate<GamePlayer> listenable,string displayName, Color radioColor)
    {
        predicate = listenable;
        this.DisplayRadioName = displayName;
        this.Color = radioColor;
    }
}

public enum VCFilteringMode
{
    All,
    AliveOnly,
    DeadOnly,
    OutOfRange,
}

[NebulaRPCHolder]
public class VoiceChatManager : IDisposable
{
    //リスナー(クライアント本人)の死亡判定
    public static bool ListenerIsPerfectlyDead => GeneralConfigurations.IsolateGhostsStrictlyOption ? (NebulaGameManager.Instance?.CanBeSpectator ?? false) : (PlayerControl.LocalPlayer?.Data?.IsDead ?? false);
    public static bool ListenerIsDead => PlayerControl.LocalPlayer?.Data?.IsDead ?? false;

    public static DataSaver VCSaver = new("VoiceChat");
    public static DataEntry<string> VCPlayerEntry = new StringDataEntry("@PlayerDevice", VCSaver, "");
    public static DataEntry<string> VCMicEntry = new StringDataEntry("@MicDevice", VCSaver, "");
    public static DataEntry<float> MasterVolumeEntry = new FloatDataEntry("@PlayerVolume", VCSaver, 1f);
    public static DataEntry<float> MicVolumeEntry = new FloatDataEntry("@MicVolume", VCSaver, 1f);
    public static DataEntry<float> MicGateEntry = new FloatDataEntry("@MicGate", VCSaver, 0.1f);

    MixingSampleProvider routeNormal, routeGhost, routeRadio, routeMixer;
    AdvancedVolumeProvider masterVolumeMixer;
    IWavePlayer myPlayer;
    public float PlayerVolume { get => MasterVolumeEntry.Value; set => MasterVolumeEntry.Value = value; }

    public VoiceChatInfo InfoShower;

    public bool IsMuting;
    public VCFilteringMode FilteringMode = VCFilteringMode.All;
    public int RadioMask;

    Dictionary<byte, VCClient> allClients = new();
    List<IVoiceComponent> allSpeakers = new();
    List<IVoiceComponent> allMics = new();
    public IEnumerable<IVoiceComponent> AllSpeakers() => allSpeakers;
    public IEnumerable<IVoiceComponent> AllMics() => allMics;
    public void AddSpeaker(IVoiceComponent speaker) => allSpeakers.Add(speaker);
    public void AddMicrophone(IVoiceComponent mic) => allMics.Add(mic);

    List<VoiceChatRadio> allRadios= new();
    VoiceChatRadio? currentRadio = null;

    private static bool AllowedUsingMic = false;
    private bool usingMic = false;
    private Coroutine? myCoroutine = null;

    public float LocalLevel = 0f;

    //DemoScreenが有効で、試聴中は自分の声が聞こえる
    public MetaScreen? DemoScreen;
    public bool DemoMode = false;
    public bool CanListenSelf { get; private set; } = false;

    public VCClient? GetClient(byte playerId) => allClients.TryGetValue(playerId, out var client) ? client : null;
    public void AddRadio(VoiceChatRadio radio)=>allRadios.Add(radio);
    public void RemoveRadio(VoiceChatRadio radio)
    {
        allRadios.Remove(radio);
        if (radio == currentRadio) currentRadio = null;
    }

    static public bool CanListenGhostVoice(GamePlayer? ghost)
    {
        if (MeetingHud.Instance || ExileController.Instance) return false;

        if (PlayerControl.LocalPlayer.Data == null) return false;

        if (PlayerControl.LocalPlayer.Data.IsDead) return false;

        var killerHearDead = GeneralConfigurations.KillersHearDeadOption.GetValue();
        if (killerHearDead == 0) return false;

        var localInfo = PlayerControl.LocalPlayer.GetModInfo();
        if (localInfo == null) return false;

        if (killerHearDead == 2)
            return localInfo.Role.Role.Category == RoleCategory.ImpostorRole;

        if (killerHearDead == 1)
            return ghost?.MyKiller == GamePlayer.LocalPlayer;

        return false;
    }
    static public bool IsInDiscussion => (MeetingHud.Instance || ExileController.Instance) && !Minigame.Instance;

    PlayersOverlay overlay;

    public VoiceChatManager()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(22050, 2);
        routeNormal = new(format) { ReadFully = true };
        routeGhost = new(format) { ReadFully = true };
        routeRadio = new(format) { ReadFully = true };
        routeMixer = new(format) { ReadFully = true };
        masterVolumeMixer = new(routeMixer, MasterVolumeEntry);

        //通常
        routeMixer.AddMixerInput(routeNormal);

        //幽霊(エフェクト)
        {
            BufferedWaveProvider reverbProvider = new(format) { ReadFully = true, BufferLength = 1 << 19 };
            
            MixingSampleProvider remixer = new(format) { ReadFully = true };
            //remixer.AddMixerInput(new ReverbSampleProvider(routeGhost));
            remixer.AddMixerInput(routeGhost);
            remixer.AddMixerInput(reverbProvider);

            SampleFunctionalProvider resampler = new(remixer, (ary, offset, count) =>
            {
                byte[] byteArray = new byte[count * 4];
                for (int i = 0; i < count; i++)
                {
                    Unsafe.As<byte, float>(ref byteArray[i * 4]) = (float)(ary[offset + i] * 0.42f);
                }
                reverbProvider.AddSamples(byteArray, 0, byteArray.Length);
            });

            reverbProvider.AddSamples(new byte[8192], 0, 8192);
            routeMixer.AddMixerInput(resampler);
        }
        
        //ラジオ
        {
            var lowPass = BiQuadFilter.LowPassFilter(22050, 2300, 1f);
            var highPass = BiQuadFilter.HighPassFilter(22050, 300, 0.8f);
            SampleFunctionalProvider radioEffector = new(routeRadio, (f) =>
            {
                f = highPass.Transform(lowPass.Transform(f));
                f = Math.Clamp(f * 1.4f, -0.28f, 0.28f) * 2.8f;
                return f;
            });
            routeMixer.AddMixerInput(radioEffector);
        }


        SetUpSoundPlayer();

        Rejoin();

        InfoShower = UnityHelper.CreateObject<VoiceChatInfo>("VCInfoShower", HudManager.Instance.transform, new Vector3(0f, 4f, -25f));

        overlay = new PlayersOverlay().Register(NebulaGameManager.Instance!).BindMask(new FunctionalMask<PlayerControl>(p => GetClient(p.PlayerId)?.IsSpeaking ?? false));
    }

    public void SetUpSoundPlayer(MMDevice? device = null)
    {
        var enumerator = new MMDeviceEnumerator();

        try
        {
            device ??= GetAllSpeakerDevice().FirstOrDefault(d => d.Id == VCPlayerEntry.Value).device;
        }
        catch { }
        device ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        
        if(myPlayer != null)
        {
            myPlayer.Pause();
            myPlayer.Dispose();
        }


        myPlayer = new WasapiOut(device, AudioClientShareMode.Shared, false, 200);
        myPlayer.Init(masterVolumeMixer);
        myPlayer.Play();
    }

    public IEnumerable<(string Id, MMDevice device)> GetAllSpeakerDevice()
    {
        foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)){
            yield return (device.DeviceFriendlyName, device);
        }
    }

    public IEnumerable<(string Id, int num)> GetAllMicDevice()
    {
        var count = WaveInEvent.DeviceCount;
        for(int i = 0; i < count; i++)
        {
            yield return (WaveInEvent.GetCapabilities(i).ProductName, i);
        }
    }

    public MixingSampleProvider GetRoute(VoiceType type)
    {
        switch (type)
        {
            case VoiceType.Radio:
                return routeRadio;
            case VoiceType.Ghost:
                return routeGhost;
            case VoiceType.Normal:
            default:
                return routeNormal;
        }
    }

    public void OnGameStart()
    {
        foreach (var c in allClients.Values) c.OnGameStart();
    }

    public void Update()
    {
        //値を毎回更新しておく
        CanListenSelf = DemoScreen && DemoMode;

        if (!GeneralConfigurations.UseVoiceChatOption)
        {
            NebulaGameManager.Instance!.VoiceChatManager = null;
            Dispose();
            return;
        }

        foreach (var key in allClients.Keys.ToArray())
        {
            var entry = allClients[key];

            if (!entry.IsValid)
            {
                entry.Dispose();
                allClients.Remove(key);
                continue;
            }
            else
            {
                entry.Update();
            }
        }

        if(PlayerControl.AllPlayerControls.Count != allClients.Count)
        {
            foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
            {
                if ((!p.gameObject.TryGetComponent<UncertifiedPlayer>(out _)) && !allClients.ContainsKey(p.PlayerId))
                {
                    allClients[p.PlayerId] = new(p);
                    allClients[p.PlayerId].SetRoute(routeNormal);
                }
            }
        }

        if (!usingMic) return;

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Mute).KeyDownForAction)
        {
            IsMuting = !IsMuting;
            InfoShower.SetMute(IsMuting);
        }

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.VCFilter).KeyDownForAction)
        {
            FilteringMode++;
            if (FilteringMode >= VCFilteringMode.OutOfRange) FilteringMode = VCFilteringMode.All;

            //生存中はフィルタ変更不可
            if (!ListenerIsPerfectlyDead) FilteringMode = VCFilteringMode.All;

            InfoShower.ShowWidget();
        }

        //生存中はフィルタ変更不可
        if (!ListenerIsPerfectlyDead) FilteringMode = VCFilteringMode.All;


        //ひとまず死んだらラジオリセット
        if (currentRadio != null && ListenerIsDead)
        {
            currentRadio = null;
            InfoShower.UnsetRadioWidget();
        }
        else
        {
            if (Input.GetKeyDown((KeyCode)(KeyCode.Alpha1)))
            {
                currentRadio = null;
                InfoShower.UnsetRadioWidget();
            }
            else
            {
                for (int i = 0; i < allRadios.Count; i++)
                {
                    if (Input.GetKeyDown((KeyCode)(KeyCode.Alpha1 + i + 1)))
                    {
                        currentRadio = allRadios[i];
                        InfoShower.SetRadioWidget(currentRadio.DisplayRadioName, currentRadio.Color);
                        break;
                    }
                }
            }
        }
    }


    public void Rejoin()
    {
        NebulaManager.Instance?.StartCoroutine(CoCommunicate().WrapToIl2Cpp());
    }

    WaveInEvent? myWaveIn = null;
    private IEnumerator CoCommunicate()
    {
        myWaveIn?.StopRecording();
        myWaveIn?.Dispose();
        myWaveIn = null;

        if (!AllowedUsingMic /*&& !AmongUsClient.Instance.AmHost*/)
        {
            var screen = MetaScreen.GenerateWindow(new(2.4f, 1f), HudManager.Instance.transform, Vector3.zero, true, true);

            MetaWidgetOld widget = new();

            widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "voiceChat.dialog.confirm" });
            widget.Append(new MetaWidgetOld.VerticalMargin(0.15f));
            widget.Append(new CombinedWidgetOld(0.45f,
                new MetaWidgetOld.Button(() => { AllowedUsingMic = true; screen.CloseScreen(); }, new(TextAttributeOld.BoldAttr) { Size = new(0.42f, 0.2f) }) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "ui.dialog.yes" },
                new MetaWidgetOld.HorizonalMargin(0.1f),
                new MetaWidgetOld.Button(() => { AllowedUsingMic = false; screen.CloseScreen(); }, new(TextAttributeOld.BoldAttr) { Size = new(0.42f, 0.2f) }) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "ui.dialog.no" }));

            screen.SetWidget(widget);

            while (screen) yield return null;

            if (!AllowedUsingMic) yield break;
        }

        //マイク使用中

        usingMic = true;

        while (!PlayerControl.LocalPlayer) { yield return new WaitForSeconds(0.5f); }

        if (!isValid) yield break;

        uint sId = GetClient(PlayerControl.LocalPlayer.PlayerId)?.sId ?? 0;

        var micName = VCMicEntry.Value;
        int deviceNumber = GetAllMicDevice().FirstOrDefault(d => d.Id == micName).num;

        OpusEncoder myEncoder = new(OpusDotNet.Application.VoIP, 24000, 1);
        myWaveIn = new WaveInEvent();
        myWaveIn.BufferMilliseconds = 100;
        myWaveIn.DeviceNumber = deviceNumber;
        myWaveIn.WaveFormat = new WaveFormat(22050, 16, 1);
        
        byte[] left = new byte[960];
        int leftLength = 0;
        byte[] opusBuffer = new byte[2048];

        List<byte[]> data = new();

        short gate = short.MaxValue;
        int tension = 0;

        myWaveIn.DataAvailable += (_, ee) =>
        {
            int read = 0;
            while (ee.BytesRecorded - read > 0)
            {
                int consumed = Math.Min(left.Length - leftLength, ee.BytesRecorded - read);
                System.Buffer.BlockCopy(ee.Buffer, read, left, leftLength, consumed);
                leftLength += consumed;
                read += consumed;

                if (leftLength == 960)
                {
                    //音量チェック
                    WaveBuffer waveBuffer = new WaveBuffer(left);
                    waveBuffer.ByteBufferCount = 960;

                    int shortLen = waveBuffer.ShortBufferCount;
                    var shortBuffer = waveBuffer.ShortBuffer;

                    bool findPeek = false;
                    float coeff = VoiceChatManager.MicVolumeEntry.Value;
                    for (int i = 0; i < shortLen; i++)
                    {
                        if (shortBuffer[i] > 0)
                            shortBuffer[i] = (short)Math.Min(shortBuffer[i] * coeff, (float)short.MaxValue);
                        else
                            shortBuffer[i] = (short)-Math.Min(-shortBuffer[i] * coeff, (float)short.MaxValue);

                        if (shortBuffer[i] > gate)
                        {
                            findPeek = true;
                            tension = 10;
                            break;
                        }
                    }

                    if (!findPeek && tension > 0) {
                        tension--;
                        findPeek = true;
                    }

                    if (findPeek)
                    {
                        var length = myEncoder.Encode(left, 960, opusBuffer, 2048);
                        var array = opusBuffer.Take(length).ToArray();
                        lock (this)
                        {
                            data.Add(array);
                        }
                    }

                    leftLength = 0;
                }
            }
        };
        myWaveIn.StartRecording();

        IEnumerator CoSend()
        {
            while (true)
            {
                if (data.Count > 0)
                {
                    lock (this)
                    {

                        if (!(IsMuting || (ListenerIsDead && !ListenerIsPerfectlyDead)))
                        {
                            foreach (var d in data)
                            {
                                RpcSendAudio.Invoke((PlayerControl.LocalPlayer.PlayerId, sId++, currentRadio != null, currentRadio?.RadioMask ?? 0, d.Length, d));
                            }
                        }
                        data.Clear();
                    }
                }
                gate = (short)(short.MaxValue * MicGateEntry.Value * 0.15f);
                yield return null;
            }
        }
        //EndCoroutine = CoSend();
        yield return CoSend();
    }

    IEnumerator? EndCoroutine = null;
    public void OnGameEndScene()
    {
        if (EndCoroutine != null) NebulaManager.Instance.StartCoroutine(EndCoroutine.WrapToIl2Cpp());
    }

    bool isValid = true;
    public void Dispose()
    {
        myPlayer?.Stop();
        myPlayer?.Dispose();
        myWaveIn?.Dispose();
        isValid = false;
    }

    static private RemoteProcess<(byte clientId,uint sId, bool isRadio,int radioMask,int dataLength,byte[] dataAry)> RpcSendAudio = new(
        "SendAudio",
        (writer,message) => {
            writer.Write(message.clientId);
            writer.Write(message.sId);
            writer.Write(message.isRadio);
            writer.Write(message.radioMask);
            writer.Write(message.dataLength);
            writer.Write(message.dataAry,0,message.dataLength);
        },
        (reader) => {
            byte id = reader.ReadByte();
            uint sId = reader.ReadUInt32();
            bool isRadio = reader.ReadBoolean();
            int radioMask = reader.ReadInt32();
            int length = reader.ReadInt32();
            return (id, sId, isRadio, radioMask, length, reader.ReadBytes(length));
            },
        (message,calledByMe) => {
            if (NebulaGameManager.Instance?.VoiceChatManager?.allClients.TryGetValue(message.clientId, out var client) ?? false)
                client?.OnReceivedData(message.sId, message.isRadio, message.radioMask, message.dataAry);
        }
        );

    public void OpenSettingScreen(OptionsMenuBehaviour menu)
    {
        var screen = MetaScreen.GenerateWindow(new Vector2(8f,4.5f), HudManager.Instance.transform, Vector3.zero, true, false, withMask: true);

        MetaWidgetOld widget = new();


        GameObject InstantiateSlideBar(Transform? parent, float value, Action<float> onVolumeChange)
        {
            var bar = GameObject.Instantiate(menu.MusicSlider, parent);
            GameObject.Destroy(bar.transform.GetChild(0).gameObject);

            var collider = bar.Bar.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 0.2f);
            collider.offset = Vector2.zero;

            bar.Bar.size = new Vector2(1f, 0.02f);
            bar.Range = new(-0.5f, 0.5f);
            bar.Bar.transform.localPosition = Vector3.zero;
            bar.Dot.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
            bar.Dot.transform.SetLocalZ(-0.1f);
            bar.transform.localPosition = new Vector3(0, -0.26f, -1f);
            bar.transform.localScale = new Vector3(1f, 1f, 1f);
            bar.SetValue(value);
            bar.OnValueChange = new();
            bar.OnValueChange.AddListener(() => onVolumeChange(bar.Value));

            return bar.gameObject;
        }

        var phoneSetting = new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left,
            GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.outputDevice"),
            GUI.API.HorizontalMargin(0.5f),
            GUI.API.Button(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), new RawTextComponent(VCPlayerEntry.Value.Length > 0 ? VCPlayerEntry.Value : Language.Translate("voiceChat.settings.device.default")), _ =>
            {
                var phonesScreen = MetaScreen.GenerateWindow(new Vector2(3f, 4.2f), HudManager.Instance.transform, Vector3.zero, true, false, withMask: true);

                var inner = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center,
                    GetAllSpeakerDevice()!.Prepend((null, null)).Select(d =>
                    GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), d.Item1 ?? Language.Translate("voiceChat.settings.device.default"),
                    _ =>
                    {
                        VCPlayerEntry.Value = d.Item1 ?? "";
                        SetUpSoundPlayer(d.Item2);
                        screen.CloseScreen();
                        phonesScreen.CloseScreen();
                        OpenSettingScreen(menu);
                    })));
                phonesScreen.SetWidget(new GUIScrollView(Virial.Media.GUIAlignment.Center, new(3f, 4.2f), inner), out var _);
            }),
            GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.outputVolume"),
            new NoSGameObjectGUIWrapper(GUIAlignment.Center, () => (InstantiateSlideBar(null, PlayerVolume * 0.5f, v => PlayerVolume = v * 2f), new(1.2f, 0.8f)))
            );

        TextMeshPro micTestText = null!;
        DemoMode = false;
        DemoScreen = screen;
        var micSetting = new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left,
            GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.inputDevice"),
            GUI.API.HorizontalMargin(0.5f),
            GUI.API.Button(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), new RawTextComponent(VCMicEntry.Value.Length > 0 ? VCMicEntry.Value : Language.Translate("voiceChat.settings.device.default")), _ =>
            {
                var micsScreen = MetaScreen.GenerateWindow(new Vector2(3f, 4.2f), HudManager.Instance.transform, Vector3.zero, true, false, withMask: true);

                var inner = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center,
                    GetAllMicDevice()!.Prepend((null, 0)).Select(d =>
                    GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), d.Item1 ?? Language.Translate("voiceChat.settings.device.default"),
                    _ =>
                    {
                        VCMicEntry.Value = d.Item1 ?? "";
                        Rejoin();
                        screen.CloseScreen();
                        micsScreen.CloseScreen();
                        OpenSettingScreen(menu);
                    })));
                micsScreen.SetWidget(new GUIScrollView(Virial.Media.GUIAlignment.Center, new(3f, 4.2f), inner), out var _);
            }),
            GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.inputVolume"),
            new NoSGameObjectGUIWrapper(GUIAlignment.Center, () => (InstantiateSlideBar(null, MicVolumeEntry.Value * 0.5f, v => MicVolumeEntry.Value = v * 2f), new(1.2f, 0.8f))),
            new GUIButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("voiceChat.settings.micTest")) { 
                PostBuilder = t => micTestText = t,
                OnClick = clickable =>
                {
                    DemoMode = !DemoMode;
                    micTestText.text = Language.Translate(DemoMode ? "voiceChat.settings.micTest.end" : "voiceChat.settings.micTest");
                }
            }
            );

        var micSettingMore = new HorizontalWidgetsHolder(GUIAlignment.Left,
            GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.noiseGate"),
            new NoSGameObjectGUIWrapper(GUIAlignment.Center, () => (InstantiateSlideBar(null, MicGateEntry.Value, v => MicGateEntry.Value = v), new(1.2f, 0.8f)))
            );

        widget.Append(new WrappedWidget(new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center, phoneSetting, micSetting, micSettingMore)));

        var nameAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.4f, 0.3f) };
        widget.Append(allClients.Values.Where(v => !v.MyPlayer.AmOwner), (client) =>
        {

            return new MetaWidgetOld.Text(nameAttr)
            {
                RawText = client.MyPlayer.name,
                PostBuilder = (text) =>
                {
                    InstantiateSlideBar(text.transform.parent, client.Volume * 0.125f, v => client.SetVolume(v * 8f));
                }
            };
        }, 5, -1, 0, 0.65f);

        screen.SetWidget(widget);
    }

    static public void RegisterRadio(ILifespan lifespan, Predicate<GamePlayer> predicate, string translationKey, Color color)
    {
        VoiceChatRadio radio = new(predicate, Language.Translate(translationKey), color);

        NebulaGameManager.Instance?.VoiceChatManager?.AddRadio(radio);
        GameOperatorManager.Instance?.RegisterReleasedAction(() => NebulaGameManager.Instance?.VoiceChatManager?.RemoveRadio(radio), lifespan);
    }
}

public class AdvancedVolumeProvider : ISampleProvider
{
    ISampleProvider sourceProvider;
    public WaveFormat WaveFormat { get => sourceProvider.WaveFormat; }
    public int Read(float[] buffer, int offset, int count)
    {
        int num = sourceProvider.Read(buffer, offset, count);

        float vol = volume.Value;
        Parallel.For(0, num, i => buffer[offset + i] *= vol);

        return num;
    }

    public AdvancedVolumeProvider(ISampleProvider sourceProvider, DataEntry<float> volume)
    {
        this.sourceProvider = sourceProvider;
        this.volume = volume;
    }

    private DataEntry<float> volume;
}

public class SampleFunctionalProvider : ISampleProvider
{
    ISampleProvider sourceProvider;
    public WaveFormat WaveFormat { get => sourceProvider.WaveFormat; }
    public int Read(float[] buffer, int offset, int count)
    {
        int num = sourceProvider.Read(buffer, offset, count);
        //for (int i = num; i < count; i++) buffer[offset + i] = 0f;
        if (OnReadArray != null) OnReadArray(buffer, offset, num);
        if(OnRead != null) for(int i = 0; i < num; i++) buffer[offset + i] = OnRead(buffer[offset + i]);
        
        return num;
    }

    public SampleFunctionalProvider(ISampleProvider sourceProvider, Func<float, float>? onRead)
    {
        this.sourceProvider = sourceProvider;
        OnRead = onRead;
    }

    public SampleFunctionalProvider(ISampleProvider sourceProvider, Action<float[],int, int>? onRead)
    {
        this.sourceProvider = sourceProvider;
        OnReadArray = onRead;
    }

    public Func<float, float>? OnRead = null;
    public Action<float[], int, int>? OnReadArray = null;
}

public class VolumeMeter : ISampleProvider
{
    private readonly ISampleProvider source;

    public WaveFormat WaveFormat => source.WaveFormat;
    private Func<bool> OnlySampleVolume;
    public float Level { get; private set; }
    public VolumeMeter(ISampleProvider source,Func<bool> onlySampleVolume)
    {
        this.source = source;
        OnlySampleVolume = onlySampleVolume;
    }

    public int Read(float[] buffer, int offset, int sampleCount)
    {
        int result = source.Read(buffer, offset, sampleCount);

        Level = 0f;
        bool onlySampleVolume = OnlySampleVolume.Invoke();
        for (int i = 0; i < sampleCount; i++)
        {
            if(buffer[offset + i] > Level) Level = buffer[offset + i];
            if (onlySampleVolume) buffer[offset + i] = 0f;
        }
        return result;
    }
}


//CircularBufferにPeekを追加したもの
public class AdvancedCircularBuffer<T>
{
    private readonly T[] buffer;

    private readonly object lockObject;

    private int writePosition;

    private int readPosition;

    private int byteCount;

    public int MaxLength => buffer.Length;

    public int Count
    {
        get
        {
            lock (lockObject)
            {
                return byteCount;
            }
        }
    }

    public AdvancedCircularBuffer(int size)
    {
        buffer = new T[size];
        lockObject = new object();
    }

    public int Write(T[] data, int offset, int count)
    {
        lock (lockObject)
        {
            int num = 0;
            if (count > buffer.Length - byteCount)
            {
                count = buffer.Length - byteCount;
            }

            int num2 = Math.Min(buffer.Length - writePosition, count);
            Array.Copy(data, offset, buffer, writePosition, num2);
            writePosition += num2;
            writePosition %= buffer.Length;
            num += num2;
            if (num < count)
            {
                Array.Copy(data, offset + num, buffer, writePosition, count - num);
                writePosition += count - num;
                num = count;
            }

            byteCount += num;
            return num;
        }
    }

    public int Read(T[] data, int offset, int count)
    {
        lock (lockObject)
        {
            if (count > byteCount)
            {
                count = byteCount;
            }

            int num = 0;
            int num2 = Math.Min(buffer.Length - readPosition, count);
            Array.Copy(buffer, readPosition, data, offset, num2);
            num += num2;
            readPosition += num2;
            readPosition %= buffer.Length;
            if (num < count)
            {
                Array.Copy(buffer, readPosition, data, offset + num, count - num);
                readPosition += count - num;
                num = count;
            }

            byteCount -= num;
            return num;
        }
    }

    public int Peek(T[] data, int offset, int count,int bufferOffset)
    {
        lock (lockObject)
        {
            if (count > byteCount)
            {
                count = byteCount;
            }

            int num = 0;
            int peekPosition = (buffer.Length + readPosition + bufferOffset) % buffer.Length;
            int num2 = Math.Min(buffer.Length - peekPosition, count);
            Array.Copy(buffer, peekPosition, data, offset, num2);
            num += num2;
            peekPosition += num2;
            peekPosition %= buffer.Length;
            if (num < count)
            {
                Array.Copy(buffer, peekPosition, data, offset + num, count - num);
                peekPosition += count - num;
                num = count;
            }

            byteCount -= num;
            return num;
        }
    }

    public void Reset()
    {
        lock (lockObject)
        {
            ResetInner();
        }
    }

    private void ResetInner()
    {
        byteCount = 0;
        readPosition = 0;
        writePosition = 0;
    }

    public void Advance(int count)
    {
        lock (lockObject)
        {
            if (count >= byteCount)
            {
                ResetInner();
                return;
            }

            byteCount -= count;
            readPosition += count;
            readPosition %= MaxLength;
        }
    }
}

public class ReverbBufferedSampleProvider : ISampleProvider
{
    private AdvancedCircularBuffer<float>? circularBuffer;

    private readonly WaveFormat waveFormat;
    public bool ReadFully { get; set; }

    public int BufferLength { get; set; }

    public TimeSpan BufferDuration
    {
        get
        {
            return TimeSpan.FromSeconds((double)BufferLength / (double)WaveFormat.AverageBytesPerSecond);
        }
        set
        {
            BufferLength = (int)(value.TotalSeconds * (double)WaveFormat.AverageBytesPerSecond);
        }
    }

    public int BufferedSamples
    {
        get
        {
            if (circularBuffer != null)
            {
                return circularBuffer.Count;
            }

            return 0;
        }
    }

    public TimeSpan BufferedDuration => TimeSpan.FromSeconds((double)BufferedSamples / (double)WaveFormat.AverageBytesPerSecond);

    public WaveFormat WaveFormat => waveFormat;

    public ReverbBufferedSampleProvider(WaveFormat waveFormat)
    {
        this.waveFormat = waveFormat;
        BufferLength = waveFormat.AverageBytesPerSecond * 1;
        ReadFully = true;
    }

    public void AddSamples(float[] buffer, int offset, int count)
    {
        circularBuffer ??= new AdvancedCircularBuffer<float>(BufferLength);

        if (circularBuffer.Write(buffer, offset, count) < count)
        {
            throw new InvalidOperationException("Advanced Circular Buffer full");
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int num = circularBuffer?.Read(buffer, offset, count) ?? 0;
        

        if (ReadFully && num < count)
        {
            Array.Clear(buffer, offset + num, count - num);
            num = count;
        }

        return num;
    }

    public int Peek(float[] buffer, int offset, int count, int bufferOffset)
    {
        int num = circularBuffer?.Peek(buffer, offset, count, bufferOffset) ?? 0;

        if (ReadFully && num < count)
        {
            Array.Clear(buffer, offset + num, count - num);
            num = count;
        }

        return num;
    }

    public void ClearBuffer()
    {
        circularBuffer?.Reset();
    } 
}

public class ReverbSampleProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get => reverb.WaveFormat; }
    private ReverbBufferedSampleProvider reverb;
    private ISampleProvider sourceProvider;

    public int Read(float[] buffer, int offset, int count)
    {
        int num = sourceProvider.Read(buffer,offset,count);
        reverb.AddSamples(buffer, offset, num);

        float[] reverbBuffer = new float[num];
        for (int n = 0; n < 7; n++)
        {
            Array.Clear(reverbBuffer);
            reverb.Peek(reverbBuffer, 0, num, -1100 * (n + 1));
            for (int i = 0; i < num; i++) buffer[i + offset] += reverbBuffer[i] * (float)Math.Pow(0.985f, (float)(n + 1.2f));
        }
        reverb.Read(reverbBuffer, 0, num);
        return num;
    }

    public ReverbSampleProvider(ISampleProvider provider)
    {
        sourceProvider = provider;
        reverb = new(sourceProvider.WaveFormat);
    }
}
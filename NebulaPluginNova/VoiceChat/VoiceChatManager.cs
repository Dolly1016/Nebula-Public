using Il2CppSystem.ComponentModel;
using Mono.Cecil.Cil;
using NAudio.CoreAudioApi;
using NAudio.Dmo.Effect;
using NAudio.Dsp;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Nebula.Behaviour;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;

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
    private Predicate<PlayerModInfo> predicate;
    public string DisplayRadioName { get; private set; }
    public Color Color { get; private set; }
    public int RadioMask
    {
        get
        {
            int mask = 0;
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo()) if (predicate.Invoke(p)) mask |= 1 << p.PlayerId;
            return mask;
        } 
    }

    public VoiceChatRadio(Predicate<PlayerModInfo> listenable,string displayName, Color radioColor)
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
    public static bool ListenerIsPerfectlyDead => GeneralConfigurations.IsolateGhostsStrictlyOption ? (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) : (PlayerControl.LocalPlayer?.Data?.IsDead ?? false);
    public static bool ListenerIsDead => PlayerControl.LocalPlayer?.Data?.IsDead ?? false;

    public static DataSaver VCSaver = new("VoiceChat");

    TcpListener? myListener = null;
    TcpClient? myClient;
    Process? childProcess;

    MixingSampleProvider routeNormal, routeGhost, routeRadio, routeMixer;
    IWavePlayer myPlayer;

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

    public VCClient? GetClient(byte playerId) => allClients.TryGetValue(playerId, out var client) ? client : null;
    public void AddRadio(VoiceChatRadio radio)=>allRadios.Add(radio);
    public void RemoveRadio(VoiceChatRadio radio)
    {
        allRadios.Remove(radio);
        if (radio == currentRadio) currentRadio = null;
    }

    static public bool CanListenGhostVoice(PlayerModInfo? ghost)
    {
        if (MeetingHud.Instance || ExileController.Instance) return false;

        if (PlayerControl.LocalPlayer.Data == null) return false;

        if (PlayerControl.LocalPlayer.Data.IsDead) return false;

        var killerHearDead = GeneralConfigurations.KillersHearDeadOption.CurrentValue;
        if (killerHearDead == 0) return false;

        var localInfo = PlayerControl.LocalPlayer.GetModInfo();
        if (localInfo == null) return false;

        if (killerHearDead == 2)
            return localInfo.Role.Role.Category == RoleCategory.ImpostorRole;

        if (killerHearDead == 1)
            return ghost?.MyKiller == NebulaGameManager.Instance?.LocalPlayerInfo;

        return false;
    }
    static public bool IsInDiscussion => (MeetingHud.Instance || ExileController.Instance) && !Minigame.Instance;
    public VoiceChatManager()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(22050, 2);
        routeNormal = new(format) { ReadFully = true };
        routeGhost = new(format) { ReadFully = true };
        routeRadio = new(format) { ReadFully = true };
        routeMixer = new(format) { ReadFully = true };

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
        

        var mmDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        myPlayer = new WasapiOut(mmDevice, AudioClientShareMode.Shared, false, 200);
        myPlayer.Init(routeMixer);
        myPlayer.Play();

        if (NebulaPlugin.MyPlugin.IsPreferential) Rejoin();

        InfoShower = UnityHelper.CreateObject<VoiceChatInfo>("VCInfoShower", HudManager.Instance.transform, new Vector3(0f, 4f, -25f));
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
        if (!GeneralConfigurations.UseVoiceChatOption)
        {
            NebulaGameManager.Instance!.VoiceChatManager = null;
            Dispose();
            return;
        }

        foreach (var entry in allClients)
        {
            if (!entry.Value.IsValid)
            {
                entry.Value.Dispose();
                allClients.Remove(entry.Key);
                continue;
            }

            entry.Value.Update();
        }

        if(PlayerControl.AllPlayerControls.Count != allClients.Count)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!allClients.ContainsKey(p.PlayerId))
                {
                    allClients[p.PlayerId] = new(p);
                    allClients[p.PlayerId].SetRoute(routeNormal);
                }
            }
        }

        if (!usingMic) return;

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Mute).KeyDown)
        {
            IsMuting = !IsMuting;
            InfoShower.SetMute(IsMuting);
        }

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.VCFilter).KeyDown)
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

    private void StartSubprocess()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        string id = process.Id.ToString();

        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "VoiceChatSupport.exe";
        processStartInfo.Arguments = (ClientOption.AllOptions[ClientOption.ClientOptionType.UseNoiseReduction].Value) + id;
        processStartInfo.CreateNoWindow = true;
        processStartInfo.UseShellExecute = false;
        childProcess = Process.Start(processStartInfo);
    }

    public void Rejoin()
    {
        try
        {
            if (myCoroutine != null) NebulaManager.Instance?.StopCoroutine(myCoroutine);
        }
        catch { }

        myCoroutine = NebulaManager.Instance?.StartCoroutine(CoCommunicate().WrapToIl2Cpp()) ?? null;
    }

    private IEnumerator CoCommunicate()
    {
        try
        {
            childProcess?.Kill();
            childProcess = null;
        }
        catch { }

        if (!AllowedUsingMic /*&& !AmongUsClient.Instance.AmHost*/)
        {
            var screen = MetaScreen.GenerateWindow(new(2.4f, 1f), HudManager.Instance.transform, Vector3.zero, true, true, false);

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

        myListener = new TcpListener(System.Net.IPAddress.Parse("127.0.0.1"), 11010);
        myListener.Start();

        StartSubprocess();
        //マイク使用中

        var task = myListener.AcceptTcpClientAsync();
        while (!task.IsCompleted) yield return new WaitForSeconds(0.4f);

        if (task.IsFaulted)
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Failed to connect.");
            yield break;
        }

        myClient = task.Result;
        NetworkStream voiceStream = myClient.GetStream();

        myListener.Stop();

        usingMic = true;

        while (!PlayerControl.LocalPlayer) { yield return new WaitForSeconds(0.5f); }

        int resSize;
        byte[] headRes = new byte[2];
        uint sId = GetClient(PlayerControl.LocalPlayer.PlayerId)?.sId ?? 0;

        while (true)
        {
            //ヘッダーを受信 (長さのみ)
            while (!voiceStream.DataAvailable) yield return null;

            var readHeaderTask = voiceStream.ReadAsync(headRes, 0, 2);
            if (!readHeaderTask.IsCompleted) yield return null;
            if (readHeaderTask.Result == 0) continue;

            resSize = BitConverter.ToInt16(headRes, 0);

            if (resSize == 0) break;

            int read = 0;
            byte[] res = new byte[resSize];
            while (read < resSize)
            {
                var readBodyTask = voiceStream.ReadAsync(res, read, resSize - read);
                if (!readBodyTask.IsCompleted) yield return null;
                read += readBodyTask.Result;
            }

            if (IsMuting) continue;

            //実際には死んでいるが、まだ復活の余地があるプレイヤーは声を誰にも届けられない
            if (ListenerIsDead && !ListenerIsPerfectlyDead) continue;

            RpcSendAudio.Invoke((PlayerControl.LocalPlayer.PlayerId, sId++, currentRadio != null, currentRadio?.RadioMask ?? 0, resSize, res));
        }
    }

    public void Dispose()
    {
        myClient?.Close();
        myClient?.Dispose();
        myListener?.Stop();
        myPlayer?.Stop();
        myPlayer?.Dispose();
        childProcess?.Kill();
        childProcess = null;

        if (myCoroutine != null) NebulaManager.Instance?.StopCoroutine(myCoroutine);
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
                client?.OnReceivedData(message.sId, message.isRadio,  message.radioMask, message.dataAry);
        }
        );


    public void OpenSettingScreen(OptionsMenuBehaviour menu)
    {
        var screen = MetaScreen.GenerateWindow(new Vector2(7f,3.2f), HudManager.Instance.transform, Vector3.zero, true, false, true);

        MetaWidgetOld widget = new();


        widget.Append(allClients.Values.Where(v=>!v.MyPlayer.AmOwner), (client) => {

        return new MetaWidgetOld.Text(TextAttributeOld.BoldAttr)
        {
            RawText = client.MyPlayer.name,
            PostBuilder = (text) =>
            {
                var bar = GameObject.Instantiate(menu.MusicSlider, text.transform.parent);
                GameObject.Destroy(bar.transform.GetChild(0).gameObject);
                
                var collider = bar.Bar.GetComponent<BoxCollider2D>();
                collider.size = new Vector2(1.2f,0.2f);
                collider.offset = Vector2.zero;

                bar.Bar.size = new Vector2(1f, 0.02f);
                bar.Range = new(-0.5f, 0.5f);
                bar.Bar.transform.localPosition = Vector3.zero;
                bar.Dot.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
                bar.Dot.transform.SetLocalZ(-0.1f);
                bar.transform.localPosition = new Vector3(0, -0.26f, -1f);
                bar.transform.localScale = new Vector3(1f, 1f, 1f);
                bar.SetValue(client.Volume * 0.25f);
                bar.OnValueChange = new();
                bar.OnValueChange.AddListener(() => client.SetVolume(bar.Value * 4f, true));
            }
        };
        }, 4, -1, 0, 0.65f);
        

        screen.SetWidget(widget);
    }

    static public NebulaGameScript GenerateBindableRadioScript(Predicate<PlayerModInfo> predicate, string translationKey, Color color)
    {
        VoiceChatRadio radio = new(predicate, Language.Translate(translationKey), color);
        return new NebulaGameScript()
        {
            OnActivatedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.AddRadio(radio),
            OnReleasedEvent = () => NebulaGameManager.Instance?.VoiceChatManager?.RemoveRadio(radio)
        };
    }
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
    private bool OnlySampleVolume;
    public float Level { get; private set; }
    public VolumeMeter(ISampleProvider source,bool onlySampleVolume)
    {
        this.source = source;
        OnlySampleVolume = onlySampleVolume;
    }

    public int Read(float[] buffer, int offset, int sampleCount)
    {
        int result = source.Read(buffer, offset, sampleCount);

        Level = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            if(buffer[offset + i] > Level) Level = buffer[offset + i];
            if (OnlySampleVolume) buffer[offset + i] = 0f;
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
using Il2CppSystem.IO;
using Interstellar;
using Interstellar.Routing;
using Interstellar.Routing.Router;
using Interstellar.VoiceChat;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using Nebula.Patches;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UIElements;
using Virial.Events.VoiceChat;
using Virial.Media;
using static Interstellar.VoiceChat.VCRoom;
using static Nebula.Modules.MetaWidgetOld;
using static UnityEngine.AudioClip;

namespace Nebula.VoiceChat;

internal class NoSVCRoom
{
    internal class VCSettings
    {
        private static DataSaver VCSaver = new("VoiceChat");
        private static DataEntry<string> VCPlayerEntry = new StringDataEntry("@PlayerDevice", VCSaver, "");
        private static DataEntry<string> VCMicEntry = new StringDataEntry("@MicDeviceName", VCSaver, Microphone.devices.Length > 0 ? Microphone.devices[0] : "");
        private static DataEntry<float> MasterVolumeEntry = new FloatDataEntry("@PlayerVolume", VCSaver, 1f);
        public static DataEntry<float> MicVolumeEntry = new FloatDataEntry("@MicVolume", VCSaver, 1f);
        //public static DataEntry<float> MicGateEntry = new FloatDataEntry("@MicGate", VCSaver, 0.1f);

        private static Dictionary<string, FloatDataEntry> playerVolumeEntries = [];
        internal static FloatDataEntry GetPlayerVolumeEntry(string puid)
        {
            if (playerVolumeEntries.TryGetValue(puid, out var entry)) return entry;
            entry = new FloatDataEntry(puid, VCSaver, 2f);
            playerVolumeEntries[puid] = entry;
            return entry;
        }
        public static float MasterVolume
        {
            get => MasterVolumeEntry.Value; set
            {
                MasterVolumeEntry.Value = value;
                ModSingleton<NoSVCRoom>.Instance?.SetMasterVolume(value);
            }
        }

        public static float MicVolume
        {
            get => MicVolumeEntry.Value; set
            {
                MicVolumeEntry.Value = value;
                ModSingleton<NoSVCRoom>.Instance?.SetMicVolume(value);
            }
        }

#if PC
        public static string? SpeakerDevice
        {
            get => VCPlayerEntry.Value; set
            {
                VCPlayerEntry.Value = value!;
                ModSingleton<NoSVCRoom>.Instance?.SetSpeaker(VCPlayerEntry.Value);
            }
        }
#endif
        public static string MicrophoneDevice
        {
            get => VCMicEntry.Value; set
            {
                VCMicEntry.Value = value;
                ModSingleton<NoSVCRoom>.Instance?.SetMicrophone(VCMicEntry.Value);
            }
        }


        public static void OpenSettingScreen(OptionsMenuBehaviour menu, MetaScreen? screen = null, bool demoMode = false)
        {
            if (screen == null)
            {
                screen ??= MetaScreen.GenerateWindow(new Vector2(8.4f, 4.2f), HudManager.Instance.transform, Vector3.zero, true, false, withMask: true);
                screen.gameObject.AddComponent<ScriptBehaviour>().DestroyHandler += () =>
                {
                    ModSingleton<NoSVCRoom>.Instance?.SetLoopBack(false);
                };
            }

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

#if PC
            var phoneSetting = new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.outputDevice"),
                GUI.API.HorizontalMargin(0.3f),
                GUI.API.Button(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), new RawTextComponent(VCPlayerEntry.Value.Length > 0 ? VCPlayerEntry.Value : Language.Translate("voiceChat.settings.device.default")), _ =>
                {
                    var phonesScreen = MetaScreen.GenerateWindow(new Vector2(3.8f, 4.2f), HudManager.Instance.transform, Vector3.zero, true, false, withMask: true);

                    var inner = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center,
                        Interstellar.AudioDevices.SpeakerDevices().Select(d =>
                        GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), d ?? Language.Translate("voiceChat.settings.device.default"),
                        _ =>
                        {
                            SpeakerDevice = d ?? null;
                            phonesScreen.CloseScreen();
                            OpenSettingScreen(menu, screen, demoMode);
                        })));
                    phonesScreen.SetWidget(new GUIScrollView(Virial.Media.GUIAlignment.Center, new(3.8f, 4.2f), inner), out var _);
                })
                ,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.outputVolume"),
                new NoSGameObjectGUIWrapper(GUIAlignment.Center, () => (InstantiateSlideBar(null, MasterVolume * 0.5f, v => MasterVolume = v * 2f), new(1.2f, 0.8f)))
                );
#endif

            TextMeshPro micTestText = null!;
            string GetTestTranslationKey() => demoMode ? "voiceChat.settings.micTest.end" : "voiceChat.settings.micTest";
            var micSetting = new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.inputDevice"),
                GUI.API.HorizontalMargin(0.3f),
                GUI.API.Button(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), new RawTextComponent(VCMicEntry.Value.Length > 0 ? VCMicEntry.Value : Language.Translate("voiceChat.settings.device.default")), _ =>
                {
                    var micsScreen = MetaScreen.GenerateWindow(new Vector2(3.8f, 4.2f), HudManager.Instance.transform, Vector3.zero, true, false, withMask: true);

                    var inner = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center,
#if ANDROID
                        Microphone.devices
#else
                        AudioDevices.MicrophoneDevices()
#endif
                        .Select(d =>
                        GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DeviceButton), d,
                        _ =>
                        {
                            MicrophoneDevice = d;
                            micsScreen.CloseScreen();
                            OpenSettingScreen(menu, screen, demoMode);
                        })));
                    micsScreen.SetWidget(new GUIScrollView(Virial.Media.GUIAlignment.Center, new(3.8f, 4.2f), inner), out var _);
                }),

                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.inputVolume"),
                new NoSGameObjectGUIWrapper(GUIAlignment.Center, () => (InstantiateSlideBar(null, MicVolume * 0.5f, v => MicVolume = v * 2f), new(1.2f, 0.8f))),

                new GUIButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), new TranslateTextComponent(GetTestTranslationKey()))
                {
                    PostBuilder = t => micTestText = t,
                    OnClick = clickable =>
                    {
                        demoMode = !demoMode;
                        ModSingleton<NoSVCRoom>.Instance?.SetLoopBack(demoMode);
                        micTestText.text = Language.Translate(GetTestTranslationKey());
                    }
                }
                );

            /*
            var micSettingMore = new HorizontalWidgetsHolder(GUIAlignment.Left,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "voiceChat.settings.noiseGate"),
                new NoSGameObjectGUIWrapper(GUIAlignment.Center, () => (InstantiateSlideBar(null, MicGateEntry.Value, v => MicGateEntry.Value = v), new(1.2f, 0.8f)))
                );
            */

            widget.Append(new WrappedWidget(new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center, micSetting/*, micSettingMore*/
#if PC
            , phoneSetting
#endif
                )));

            var nameAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.4f, 0.3f) };
            widget.Append(ModSingleton<NoSVCRoom>.Instance?.AllClients ?? [], (client) =>
            {

                return new MetaWidgetOld.Text(nameAttr)
                {
                    RawText = client.PlayerName,
                    PostBuilder = (text) =>
                    {
                        InstantiateSlideBar(text.transform.parent, client.Volume * 0.25f, v => client.SetVolume(v * 4f));
                    }
                };
            }, 5, -1, 0, 0.65f);


            screen.SetWidget(widget);
        }
    }


    internal class VCPlayer
    {
        private class Mapping
        {
            private int playerId = -1;
            private string playerName = null!;
            private PlayerControl mappedPlayer = null!;
            private GamePlayer? mappedModPlayer = null!;
            public string CachedPlayerName => playerName ?? "Unknown";
            public bool IsMapped => HasBeenMapped || mappedPlayer != null && mappedPlayer;
            private bool HasBeenMapped = false;
            private FloatDataEntry? volumeEntry = null;
            public GamePlayer? MappedModPlayer => mappedModPlayer;
            public void UpdateProfile(int playerId, string playerName)
            {
                this.playerId = playerId;
                this.playerName = playerName;
                this.mappedPlayer = null!;
                this.mappedModPlayer = null!;
            }

            public void CheckMappedPlayer()
            {
                if (playerId < 0) return;

                if (mappedPlayer != null && !mappedPlayer)
                {
                    mappedPlayer = null!;
                    mappedModPlayer = null!;
                }

                if (mappedPlayer == null && (LobbyBehaviour.Instance || ShipStatus.Instance))
                {
                    mappedPlayer = PlayerControl.AllPlayerControls.GetFastEnumerator().FirstOrDefault(p => p.PlayerId == playerId)!;

                    IEnumerator CoSetVolumeEntry(PlayerControl player)
                    {
                        string puid = "";
                        while (puid.Length == 0)
                        {
                            yield return new WaitForSeconds(1f);
                            if (!player) yield break;
                            yield return PropertyRPC.CoGetProperty<string>(player.PlayerId, "myPuid", result => puid = result, null);
                        }
                        Debug.Log($"Gain PUID of {player.name} ({player.PlayerId} : {puid})");
                        if (puid.Length == 0) puid = player.name;
                        volumeEntry = VCSettings.GetPlayerVolumeEntry(puid);
                        if(ModSingleton<NoSVCRoom>.Instance.TryGetPlayer(player.PlayerId, out var p)) p.SetVolume(volumeEntry.Value);
                    }
                    if(mappedPlayer) NebulaManager.Instance.StartCoroutine(CoSetVolumeEntry(mappedPlayer).WrapToIl2Cpp());
                }

                if (mappedModPlayer == null && mappedPlayer)
                {
                    if (mappedPlayer && ShipStatus.Instance) mappedModPlayer = mappedPlayer.GetModInfo();
                }
            }

            internal void UpdateMappedState()
            {
                HasBeenMapped = false;
                HasBeenMapped = IsMapped;
            }

            internal void UpdateVolumeEntry(float volume)
            {
                if(volumeEntry != null) volumeEntry.Value = volume;
            }

            public string PlayerName => playerName ?? "Unknown";
            public byte PlayerId => (byte)playerId;
        }
        private NoSVCRoom? Room;
        private Mapping mapping = new();
        private StereoRouter.Property imager, droneImager;
        private VolumeRouter.Property clientVolume, normalVolume, ghostVolume, radioVolume, droneVolume;
        private LevelMeterRouter.Property levelMeter;
        public string PlayerName => mapping.PlayerName;
        public byte PlayerId => mapping.PlayerId;
        public float Volume => clientVolume.Volume;
        public float Level => levelMeter.Level;

        public VCPlayer(NoSVCRoom vcRoom, AudioRoutingInstance instance)
        {
            Room = vcRoom;
            this.imager = Room.imager.GetProperty(instance);
            this.droneImager = Room.droneImager.GetProperty(instance);
            this.normalVolume = Room.normalVolume.GetProperty(instance);
            this.ghostVolume = Room.ghostVolume.GetProperty(instance);
            this.radioVolume = Room.radioVolume.GetProperty(instance);
            this.droneVolume = Room.droneVolume.GetProperty(instance);
            this.clientVolume = Room.clientVolume.GetProperty(instance);
            this.levelMeter = Room.levelMeter.GetProperty(instance);
            this.clientVolume.Volume = 1f;

            MuteAll();
        }

        private void MuteAll()
        {
            normalVolume.Volume = 0f;
            ghostVolume.Volume = 0f;
            radioVolume.Volume = 0f;
            droneVolume.Volume = 0f;
        }

        internal void UpdateProfile(byte playerId, string playerName)
        {
            mapping.UpdateProfile(playerId, playerName);
            MuteAll();
        }

        internal void Update(Vector2? position, IEnumerable<SpeakerCache> speakers, bool canIgnoreWalls)
        {
            mapping.CheckMappedPlayer();
            if (!mapping.IsMapped)
            {
                MuteAll();
                return;
            }

            var gameState = AmongUsClient.Instance.GameState;
            var inLobby = gameState < InnerNet.InnerNetClient.GameStates.Started || (gameState == InnerNet.InnerNetClient.GameStates.Ended && !HudManager.InstanceExists);
            if (inLobby)
            {
                UpdateLobby();
                return;
            }
            var inMeeting = MeetingHud.Instance || ExileController.Instance;
            if (inMeeting)
            {
                UpdateMeeting();
                return;
            }

            UpdateTaskPhase(position, speakers, canIgnoreWalls);
        }

        private void UpdateLobby()
        {
            imager.Pan = 0f;
            normalVolume.Volume = 1f;
            ghostVolume.Volume = 0f;
            radioVolume.Volume = 0f;
            droneVolume.Volume = 0f;
        }

        private void UpdateMeeting()
        {
            var localIsDead = GamePlayer.LocalPlayer?.IsDead ?? false;
            var targetIsDead = mapping.MappedModPlayer?.IsDead ?? true;

            bool canHear = localIsDead || !targetIsDead;

            imager.Pan = 0f;
            normalVolume.Volume = canHear ? 1f : 0f;
            ghostVolume.Volume = 0f;
            radioVolume.Volume = 0f;
            droneVolume.Volume = 0f;
        }

        float wallCoeff = 1f;
        private void UpdateTaskPhase(Vector2? hearingPosition, IEnumerable<SpeakerCache> speakers, bool canIgnoreWalls)
        {
            var localPlayer = GamePlayer.LocalPlayer;
            var localIsDead = localPlayer?.IsDead ?? false;
            var targetIsDead = mapping.MappedModPlayer?.IsDead ?? true;

            if (GeneralConfigurations.CanTalkInWanderingPhaseOption)
            {
                var affectedByCommSab = !(localPlayer?.IsDead ?? false) && GeneralConfigurations.AffectedByCommsSabOption && !(localPlayer?.IsImpostor ?? false) && AmongUsUtil.InCommSab;

                if (affectedByCommSab)
                {
                    normalVolume.Volume = 0f;
                    ghostVolume.Volume = 0f;
                    radioVolume.Volume = 0f;
                    droneVolume.Volume = 0f;
                }
                

                var target = mapping.MappedModPlayer;

                if (target != null && hearingPosition.HasValue && GamePlayer.LocalPlayer?.VanillaPlayer && target.VanillaPlayer)
                {
                    var targetPos = GameOperatorManager.Instance?.Run<FixSpeakerPositionEvent>(new(target, target.Position), true)?.Position ?? target.Position;

                    //ドローン系の音量
                    {
                        var pan = 0f;
                        var volMax = 0f;
                        var volSum = 0f;
                        foreach (var speaker in speakers)
                        {
                            //スピーカーが発する音声の大きさを計算
                            float volume = 0f;
                            foreach (var mic in this.Room?.virtualMicrophones ?? [])
                            {
                                if (!speaker.Speaker.CanPlaySoundFrom(mic)) continue;
                                float v = mic.CanCatch(target, targetPos);
                                if (v > volume) volume = v;
                            }

                            if (volume > 0f)
                            {
                                float realVol = speaker.Volume * volume;
                                if (realVol > volMax) volMax = realVol;

                                pan += speaker.Pan * realVol;
                                volSum += realVol;
                            }
                        }
                        //重み付き平均を計算するため、重みの和で割る。
                        pan /= volSum;

                        droneImager.Pan = pan;
                        droneVolume.Volume = volMax;
                    }

                    //本体の音量
                    {
                        var volume = GetVolume(targetPos.Distance(hearingPosition.Value), HearDistance);
                        var pan = GetPan(hearingPosition.Value.x, targetPos.x);

                        imager.Pan = pan;
                        if (localIsDead)
                        {
                            normalVolume.Volume = volume;
                            ghostVolume.Volume = 0f;
                        }
                        else
                        {
                            CalcWall(ref wallCoeff, targetPos, hearingPosition.Value, canIgnoreWalls);
                            
                            normalVolume.Volume = targetIsDead ? 0f : (volume * wallCoeff);

                            if (targetIsDead)
                            {
                                switch (GeneralConfigurations.KillersHearDeadOption.GetValue())
                                {
                                    case 0: //Off
                                        ghostVolume.Volume = 0f;
                                        break;
                                    case 1: //OnlyMyKiller
                                        ghostVolume.Volume = (target?.MyKiller?.AmOwner ?? false) ? volume : 0f;
                                        break;
                                    case 2: //OnlyImpostors
                                        ghostVolume.Volume = (localPlayer?.IsImpostor ?? false) ? volume : 0f;
                                        break;
                                    case 3: //OnlyAllKillers
                                        ghostVolume.Volume = (localPlayer?.IsKiller ?? false) ? volume : 0f;
                                        break;
                                }
                            }
                            else
                            {
                                ghostVolume.Volume = 0f;
                            }
                        }
                    }
                }
            }
            else
            {
                imager.Pan = 0f;
                normalVolume.Volume = targetIsDead && localIsDead ? 1f : 0f;
                ghostVolume.Volume = 0f;
                radioVolume.Volume = 0f;
                droneVolume.Volume = 0f;
            }
        }

        internal void SetVolume(float volume)
        {
            clientVolume.Volume = volume;
            mapping?.UpdateVolumeEntry(volume);
        }

        internal void UpdateMappedState() => mapping.UpdateMappedState();
        
    }

    private static void CalcWall(ref float coeff, Vector2 source, Vector2 target, bool canIgnoreWalls)
    {
        bool wall = !canIgnoreWalls && NebulaPhysicsHelpers.AnyShadowBetween(source, target, out _);
        coeff -= (coeff - (wall ? 0f : 1f)).Delta(4f, 0.01f);
    }
    private bool IsActive = true;
    private VCRoom interstellarRoom;
    private Dictionary<int, VCPlayer> clients = [];
    private IEnumerable<VCPlayer> AllClients => clients.Values;
    /// <summary>
    /// 通常音声・幽霊音声のステレオイメージャ
    /// </summary>
    private StereoRouter imager, droneImager;
    private VolumeRouter clientVolume, normalVolume, ghostVolume, radioVolume, droneVolume;
    private LevelMeterRouter levelMeter;
    private VolumeRouter.Property masterVolume;

    private List<IVoiceComponent> virtualMicrophones = [], virtualSpeakers = [];
    internal void AddVirtualMicrophone(IVoiceComponent microphone) => virtualMicrophones.Add(microphone);
    internal void AddVirtualSpeaker(IVoiceComponent speaker) => virtualSpeakers.Add(speaker);
    internal void RemoveVirtualMicrophone(IVoiceComponent microphone) => virtualMicrophones.Remove(microphone);
    internal void RemoveVirtualSpeaker(IVoiceComponent speaker) => virtualSpeakers.Remove(speaker);

    static internal void StartVoiceChat(string region, string roomCode)
    {
        new NoSVCRoom(region, roomCode);
    }

    private NoSVCRoom(string region, string roomCode)
    {
        if (ModSingleton<NoSVCRoom>.Instance != null) ModSingleton<NoSVCRoom>.Instance.Close();
        ModSingleton<NoSVCRoom>.Instance = this;
        SimpleRouter source = new();
        SimpleEndpoint endpoint = new();

        imager = new();
        droneImager = new();
        normalVolume = new();
        ghostVolume = new();
        radioVolume = new();
        droneVolume = new();
        levelMeter = new();
        clientVolume = new();
        FilterRouter ghostLowpass = FilterRouter.CreateLowPassFilter(1900f, 2f);
        ReverbRouter ghostMasterReverb1 = new(53, 0.7f, 0.2f) { IsGlobalRouter = true };
        ReverbRouter ghostMasterReverb2 = new(173, 0.4f, 0.6f) { IsGlobalRouter = true };
        FilterRouter radioHighpass = FilterRouter.CreateHighPassFilter(650f, 3.2f);
        FilterRouter radioLowpass = FilterRouter.CreateLowPassFilter(800f, 2.1f);
        DistortionFilter radioDistortion = new() { IsGlobalRouter = true, DefaultThreshold = 0.55f };
        DistortionFilter droneDistortion = new() { DefaultThreshold = 0.55f };
        VolumeRouter masterVolumeRouter = new() { IsGlobalRouter = true };

        source.Connect(clientVolume);

        clientVolume.Connect(imager);
            imager.Connect(normalVolume);
                normalVolume.Connect(levelMeter);
                    levelMeter.Connect(masterVolumeRouter);
            imager.Connect(ghostLowpass);
                ghostLowpass.Connect(ghostVolume);
                    ghostVolume.Connect(ghostMasterReverb1);
                        ghostMasterReverb1.Connect(ghostMasterReverb2);
                            ghostMasterReverb2.Connect(masterVolumeRouter);
        clientVolume.Connect(radioHighpass);
            radioHighpass.Connect(radioLowpass);
                radioLowpass.Connect(droneDistortion);
                    droneDistortion.Connect(droneImager);
                        droneImager.Connect(droneVolume);
                            droneVolume.Connect(masterVolumeRouter);
                    radioLowpass.Connect(radioVolume);
                            radioVolume.Connect(radioDistortion);
                                radioDistortion.Connect(masterVolumeRouter);

        masterVolumeRouter.Connect(endpoint);
       
        interstellarRoom = new VCRoom(source, roomCode, region, "ws://www.nebula-on-the-ship.com:22010/vc",
            new VCRoomParameters()
            {
                OnConnectClient = (clientId, instance, isLocal) =>
                {
                    if (isLocal)
                    {
                        clientVolume.GetProperty(instance).Volume = 1f;
                        normalVolume.GetProperty(instance).Volume = 1f;
                    }
                    else
                    {
                        clients[clientId] = new VCPlayer(this, instance);
                    }
                },
                OnUpdateProfile = (clientId, playerId, playerName) =>
                {
                    if (clients.TryGetValue(clientId, out var player)) player.UpdateProfile(playerId, playerName);
                },
                OnDisconnect = (clientId) =>
                {
                    if (clients.TryGetValue(clientId, out var player))
                    {
                        clients.Remove(clientId);
                    }
                },
            }.SetBufferLength(2048 *
#if ANDROID 
            9
#else
            1
#endif
            )
        );

        masterVolume = masterVolumeRouter.GetProperty(interstellarRoom);
        SetMasterVolume(VCSettings.MasterVolume);
        SetMicrophone(VCSettings.MicrophoneDevice);

#if ANDROID
        var audioSource = ModSingleton<ResidentBehaviour>.Instance.gameObject.AddComponent<AudioSource>();
        audioSource.MarkDontUnload();
        var speaker = new ManualSpeaker(() =>
        {
            if (audioSource) GameObject.Destroy(audioSource);
        });
        AudioClip myClip = AudioClip.Create("VCAudio", (int)(interstellarRoom.SampleRate * 0.5f), 2, (int)interstellarRoom.SampleRate, true,
            (AudioClip.PCMReaderCallback)((ary) => speaker.Read(ary)));
        audioSource.clip = myClip;
        audioSource.loop = true;
        audioSource.Play();

        SetSpeaker(speaker);
#else
        SetSpeaker(VCSettings.SpeakerDevice);
#endif


        CoShareProfile().StartOnScene();
    }

    private static bool UserAllowedUsingMic = false;
    private static void CheckAndShowConfirmPopup(Action action)
    {
        if (UserAllowedUsingMic)
        {
            action.Invoke();
            return;
        }
        MetaUI.ShowYesOrNoDialog(HudManager.InstanceExists ? HudManager.Instance.transform : null,
            ()=> {
                UserAllowedUsingMic = true;
                action.Invoke();
            }, () => { }, Language.Translate("voiceChat.dialog.confirm"), true);
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMicrophone(string device) => this.SetMicrophone(new ManualMicrophone());
    public void SetMicrophone(ManualMicrophone microphone)
    {
        CheckAndShowConfirmPopup(() => {
            this.unityMic = microphone;
            interstellarRoom.Microphone = microphone;
        });
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMicrophone(string device) => this.SetMicrophone(new WindowsMicrophone(device));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetSpeaker(string device) => this.SetSpeaker(new WindowsSpeaker(device));
    public void SetMicrophone(IMicrophone microphone)
    {
        CheckAndShowConfirmPopup(() => interstellarRoom.Microphone = microphone);
    }
#endif
    public void SetSpeaker(ISpeaker speaker) => interstellarRoom.Speaker = speaker;

    public bool TryGetPlayer(byte playerId, [MaybeNullWhen(false)]out VCPlayer player)
    {
        foreach(var c in clients.Values)
        {
            if(c.PlayerId == playerId)
            {
                player = c;
                return true;
            }
        }
        player = null;
        return false;
    }
    public void SetClientVolume(int clientId, float volume)
    {
        if (clients.TryGetValue(clientId, out var player)) player.SetVolume(volume);
    }
    internal IEnumerator CoShareProfile()
    {
        while (!PlayerControl.LocalPlayer) yield return null;
        LogUtils.WriteToConsole("Sharing profile to voice chat server: " + PlayerControl.LocalPlayer.name  + ", ID :" + PlayerControl.LocalPlayer.PlayerId);
        interstellarRoom.UpdateProfile(PlayerControl.LocalPlayer.name, PlayerControl.LocalPlayer.PlayerId);
    }

    internal void SetMasterVolume(float volume)
    {
        masterVolume.Volume = volume;
    }

    internal void SetMicVolume(float volume)
    {
        interstellarRoom.Microphone?.SetVolume(volume);
    }

    internal void SetLoopBack(bool loopback)
    {
        interstellarRoom.SetLoopBack(loopback);
    }

    private void Close()
    {
        interstellarRoom.Disconnect();
        IsActive = false;
    }

    internal static void CloseCurrentRoom()
    {
        if (ModSingleton<NoSVCRoom>.Instance != null)
        {
            ModSingleton<NoSVCRoom>.Instance.Close();
            ModSingleton<NoSVCRoom>.Instance = null;
        }
    }

    private static float HearDistance => LightPatch.LastCalculatedRange;
    private static float GetVolume(float distance, float hearDistance) => Mathn.Clamp(1f - distance / hearDistance, 0f, 1f);
    private static float GetPan(float micX, float speakerX)
    {
        var delta = speakerX - micX;
        return Mathn.Clamp(delta / 3f, -1f, 1f);
    }

    internal record SpeakerCache(IVoiceComponent Speaker, float Volume, float Pan);
    private List<SpeakerCache> _speakerCache = [];

#if ANDROID
    int? lastPosition = null;
    string currentMic = null!;
    AudioClip? micAudioClip = null;
    ManualMicrophone? unityMic = null;
    internal bool SetUnityMicrophone(string device)
    {
        if (Microphone.devices.Contains(device))
        {
            micAudioClip = Microphone.Start(device, true, 1, 48000);
            currentMic = device;
            lastPosition = null;
            return true;
        }
        micAudioClip = null!;
        currentMic = null!;
        return false;
    }

    private void PushAudioData()
    {
        if (currentMic == null) return;
        int currentPosition = Microphone.GetPosition(currentMic);
        if (!lastPosition.HasValue)
        {
            lastPosition = currentPosition;
            return;
        }
        int sampleCount;
        if (currentPosition > lastPosition) sampleCount = currentPosition - lastPosition.Value;
        else if (currentPosition != lastPosition) sampleCount = micAudioClip!.samples - lastPosition.Value + currentPosition;
        else return;

        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> audioData = new((long)sampleCount);
        micAudioClip!.GetData(audioData, lastPosition.Value);
        lastPosition = currentPosition;
        unityMic?.PushAudioData(audioData);
    }
#endif
    private void UpdateInternal()
    {
#if ANDROID
        PushAudioData();
#endif
        float hearDistance = HearDistance;
        var localPlayer = GamePlayer.LocalPlayer;
        var camTarget = HudManager.InstanceExists ? AmongUsUtil.CurrentCamTarget : null;
        Vector2? position = camTarget ? camTarget!.transform.position : (localPlayer?.VanillaPlayer ? localPlayer!.Position : null);
        bool canIgnoreWalls = false;
        if (position.HasValue)
        {
            if (localPlayer != null)
            {
                var ev = GameOperatorManager.Instance?.Run<FixMicPositionEvent>(new(localPlayer, position.Value, localPlayer.EyesightIgnoreWalls), true);
                position = ev?.Position ?? position;
                canIgnoreWalls = ev?.CanIgnoreWalls ?? false;
            }

            _speakerCache.Clear();
            foreach (var v in virtualSpeakers)
            {
                float d = v.Position.Distance(position.Value);
                if (d < hearDistance)
                {
                    _speakerCache.Add(new(v, GetVolume(d, hearDistance), GetPan(position.Value.x, v.Position.x)));
                }
            }
        }
        else if (_speakerCache.Count > 0)
        {
            _speakerCache.Clear();
        }

        foreach (var c in clients.Values) c.Update(position, _speakerCache, canIgnoreWalls);
    }

    internal void OnGameStart()
    {
        foreach (var c in clients.Values) c.UpdateMappedState();
    }

    static internal void Update()
    {

        bool shouldNotUseVC = !GeneralConfigurations.UseVoiceChatOption || AmongUsUtil.IsLocalServer();
        bool shouldUseVC = GeneralConfigurations.UseVoiceChatOption && !AmongUsUtil.IsLocalServer();

        if (shouldNotUseVC)
        {
            if (ModSingleton<NoSVCRoom>.Instance != null) NoSVCRoom.CloseCurrentRoom();
            return;
        }
        if (shouldUseVC)
        {
            if (ModSingleton<NoSVCRoom>.Instance == null)
            {
                string region = AmongUsClient.Instance.networkAddress;
                string roomId = AmongUsClient.Instance.GameId.ToString();
                NoSVCRoom.StartVoiceChat(region, roomId);
            }

            ModSingleton<NoSVCRoom>.Instance?.UpdateInternal();
        }
    }
}
using Interstellar;
using Interstellar.Routing;
using Interstellar.Routing.Router;
using Interstellar.VoiceChat;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
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
using UnityEngine;
using Virial;
using Virial.Components;
using Virial.Events.Game.Meeting;
using Virial.Events.VoiceChat;
using Virial.Media;
using Virial.Text;
using static Interstellar.VoiceChat.VCRoom;
using static Nebula.Modules.MetaWidgetOld;
using static Rewired.Data.Player_Editor;
using static UnityEngine.AudioClip;

namespace Nebula.VoiceChat;

[NebulaRPCHolder]
internal class NoSVCRoom
{
    internal class VCSettings
    {
        private static DataSaver VCSaver = new("VoiceChat");
        private static DataEntry<string> VCPlayerEntry = new StringDataEntry("@PlayerDevice", VCSaver, "");
        private static DataEntry<string> VCMicEntry = new StringDataEntry("@MicDeviceName", VCSaver, Microphone.devices.Length > 0 ? Microphone.devices[0] : "");
        private static DataEntry<string> VCServerEntry = new StringDataEntry("@VCServer", VCSaver, "");
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

        public static string ServerAddress => VCServerEntry.Value;
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

        internal static void OpenServerSettingScreen(OptionsMenuBehaviour menu)
        {
            var window = MetaScreen.GenerateWindow(new(5f, 1.35f), null, Vector3.zero, true, false);

            var field = new GUITextField(GUIAlignment.Center, new(4.8f, 0.4f)) { IsSharpField = false, MaxLines = 1, HintText = "ws://<address>:<port>".Color(Color.gray), DefaultText = VCSettings.VCServerEntry.Value, WithMaskMaterial = false };
            window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center, [
                GUI.API.LocalizedText(GUIAlignment.Center, AttributeAsset.OverlayBold, "voiceChat.settings.server"),
                field,
                GUI.API.LocalizedButton(GUIAlignment.Center, AttributeAsset.OverlayBold, "voiceChat.settings.server.apply", _ =>
                {
                    VCSettings.VCServerEntry.Value = field.Artifact.FirstOrDefault()?.Text ?? "";
                    window.CloseScreen();
                })
                ]), new Vector2(0.5f, 1f), out _);

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
            public string CachedPlayerName => mappedPlayer ? mappedPlayer.name : playerName ?? "Unknown";
            public bool IsMapped => HasBeenMapped || mappedPlayer != null && mappedPlayer;
            private bool HasBeenMapped = false;
            private FloatDataEntry? volumeEntry = null;
            public GamePlayer? MappedModPlayer => mappedModPlayer;
            public PlayerControl MappedPlayerControl => mappedPlayer;
            public void UpdateProfile(int playerId, string playerName)
            {
                this.playerId = playerId;
                this.playerName = playerName;
                ResetMapping();
            }

            public void ResetMapping()
            {
                this.mappedPlayer = null!;
                this.mappedModPlayer = null!;
            }

            public void CheckMappedPlayer()
            {
                if (playerId < 0) return;

                if (mappedPlayer == null || mappedPlayer != null && (!mappedPlayer || mappedPlayer.PlayerId != playerId))
                {
                    ResetMapping();
                }

                if (mappedPlayer == null && (LobbyBehaviour.Instance || ShipStatus.Instance))
                {
                    mappedPlayer = PlayerControl.AllPlayerControls.GetFastEnumerator().FirstOrDefault(p => p.PlayerId == playerId)!;

                    IEnumerator CoSetVolumeEntry(PlayerControl player)
                    {
                        string puid = "";
                        int trial = 0;
                        /*
                        while (puid.Length == 0 && trial < 4)
                        {
                            if (!player) yield break;
                            LogUtils.WriteToConsole($"Try Get PUID of {player.name} ({player.PlayerId})");
                            yield return PropertyRPC.CoGetProperty<string>(player.PlayerId, "myPuid", result => puid = result, null);
                            yield return new WaitForSeconds(1f);
                            trial++;
                        }
                        */
                        LogUtils.WriteToConsole($"Gain PUID of {player.name} ({player.PlayerId} : {puid})");
                        if (puid.Length == 0) puid = player.name;
                        volumeEntry = VCSettings.GetPlayerVolumeEntry(puid);
                        if(ModSingleton<NoSVCRoom>.Instance.TryGetPlayer(player.PlayerId, out var p)) p.SetVolume(volumeEntry.Value);
                        yield break;
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
        public bool IsMapped => mapping.IsMapped;
        internal PlayerControl MappedPlayerControl => mapping.MappedPlayerControl;
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

            var gameState = AmongUsClient.Instance != null ? AmongUsClient.Instance.GameState : InnerNet.InnerNetClient.GameStates.NotJoined;
            var inLobby = gameState < InnerNet.InnerNetClient.GameStates.Started || (gameState == InnerNet.InnerNetClient.GameStates.Ended && !HudManager.InstanceExists);
            var inIntro = GameManager.Instance != null && GameManager.Instance && !GameManager.Instance.GameHasStarted;
            if (inLobby || inIntro)
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

        public void ResetMapping() => mapping.ResetMapping();

        private void UpdateLobby()
        {
            imager.Pan = 0f;
            normalVolume.Volume = 1f;
            ghostVolume.Volume = 0f;
            radioVolume.Volume = 0f;
            droneVolume.Volume = 0f;
        }

        private bool IsInRadio(GamePlayer? player, [MaybeNullWhen(false)] out VoiceState state)
        {
            state = null;
            return (Room?.voiceStates.TryGetValue(player?.PlayerId ?? byte.MaxValue, out state) ?? false) && state.IsRadio;
        }

        private void CheckAndReflectProperties(VoiceUpdateEvent voiceEvent)
        {
            if (voiceEvent.Player != null) GameOperatorManager.Instance?.Run<VoiceUpdateEvent>(voiceEvent);

            this.normalVolume.Volume = voiceEvent.NormalVolume;
            this.imager.Pan = voiceEvent.NormalPan;
            this.ghostVolume.Volume = voiceEvent.GhostVolume;
            this.radioVolume.Volume = voiceEvent.RadioVolume;
            this.droneVolume.Volume = voiceEvent.DroneVolume;
            this.droneImager.Pan = voiceEvent.DronePan;
        }

        private void CheckAndReflectProperties(bool inMeeting, float normalVolume, float normalPan, float ghostVolume, float radioVolume, float droneVolume, float dronePan)
        {
            var mappedPlayer = mapping.MappedModPlayer;
            var voiceEvent = new VoiceUpdateEvent(mappedPlayer!, true, normalVolume, normalPan, ghostVolume, radioVolume, droneVolume, dronePan);
            CheckAndReflectProperties(voiceEvent);
        }

        private void UpdateMeeting()
        {
            var mappedPlayer = mapping.MappedModPlayer;
            var localIsDead = GamePlayer.LocalPlayer?.IsDead ?? false;
            var targetIsDead = mappedPlayer?.IsDead ?? true;

            bool canHear = (localIsDead || !targetIsDead);

            CheckAndReflectProperties(true, canHear ? 1f : 0f, 0f, 0f, 0f, 0f, 0f);
        }

        float wallCoeff = 1f;
        private void UpdateTaskPhase(Vector2? hearingPosition, IEnumerable<SpeakerCache> speakers, bool canIgnoreWalls)
        {
            var mappedPlayer = mapping.MappedModPlayer;
            var localPlayer = GamePlayer.LocalPlayer;
            var localIsDead = localPlayer?.IsDead ?? false;
            var targetIsDead = mapping.MappedModPlayer?.IsDead ?? true;

            if (GeneralConfigurations.CanTalkInWanderingPhaseOption)
            {

                if (IsInRadio(mapping.MappedModPlayer, out var state))
                {
                    CheckAndReflectProperties(false, 0f, 0f, 0f, state.CanHear ? 1f : 0f, 0f, 0f);
                    return;
                }

                radioVolume.Volume = 0f;

                var affectedByCommSab = !(localPlayer?.IsDead ?? false) && GeneralConfigurations.AffectedByCommsSabOption && !(localPlayer?.IsImpostor ?? false) && AmongUsUtil.InCommSab;

                if (affectedByCommSab)
                {
                    CheckAndReflectProperties(false, 0f, 0f, 0f, 0f, 0f, 0f);
                    return;
                }
                

                var target = mapping.MappedModPlayer;

                VoiceUpdateEvent ev = new(target!, false, 0f, 0f, 0f, 0f, 0f, 0f);

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

                        ev.DronePan = pan;
                        ev.DroneVolume = volMax;
                    }

                    //本体の音量
                    {
                        var volume = GetVolume(targetPos.Distance(hearingPosition.Value), HearDistance);
                        var pan = GetPan(hearingPosition.Value.x, targetPos.x);

                        ev.NormalPan = pan;
                        if (localIsDead)
                        {
                            ev.NormalVolume = volume;
                            ev.GhostVolume = 0f;
                        }
                        else
                        {
                            CalcWall(ref wallCoeff, targetPos, hearingPosition.Value, canIgnoreWalls);

                            ev.NormalVolume = targetIsDead ? 0f : (volume * wallCoeff);

                            if (targetIsDead)
                            {
                                switch (GeneralConfigurations.KillersHearDeadOption.GetValue())
                                {
                                    case 0: //Off
                                        ev.GhostVolume = 0f;
                                        break;
                                    case 1: //OnlyMyKiller
                                        ev.GhostVolume = (target?.MyKiller?.AmOwner ?? false) ? volume : 0f;
                                        break;
                                    case 2: //OnlyImpostors
                                        ev.GhostVolume = (localPlayer?.IsImpostor ?? false) ? volume : 0f;
                                        break;
                                    case 3: //OnlyAllKillers
                                        ev.GhostVolume = (localPlayer?.IsKiller ?? false) ? volume : 0f;
                                        break;
                                }
                            }
                            else
                            {
                                ev.GhostVolume = 0f;
                            }
                        }
                    }
                }
                CheckAndReflectProperties(ev);
            }
            else
            {
                CheckAndReflectProperties(false, targetIsDead && localIsDead ? 1f : 0f, 0f, 0f, 0f, 0f, 0f);
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
    private record VoiceState(bool IsRadio, bool CanHear);
    private Dictionary<int, VoiceState> voiceStates = [];
    public bool UsingMicrophone => interstellarRoom.Microphone != null;
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

        string server = VCSettings.ServerAddress;
        if (server.Length == 0) server = "ws://www.nebula-on-the-ship.com:22010";
        interstellarRoom = new VCRoom(source, roomCode, region, server + "/vc",
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
        RegisterWidget(Language.Translate("voiceChat.info.mute"), new(1f,0.2f,0.2f), null, new FunctionalLifespan(() => true), () => interstellarRoom.Mute, 100);

        /*
        DebugScreen.Push(new FunctionalDebugTextContent(() =>
        {
            return string.Join("<br>", clients.Select(entry => entry.Value.PlayerName + ": " + entry.Value.Level));
        }, NebulaAPI.CurrentGame));
        */
    }

    private void UpdateVoiceChatInfo()
    {
        if (ModSingleton<VoiceChatInfo>.Instance == null)
        {
            var vcInfo = UnityHelper.CreateObject<VoiceChatInfo>("VCInfo", HudManager.InstanceExists ? HudManager.Instance.transform : null, new Vector3(0f, 4f, -400f));
        }
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

    internal void Rejoin()
    {
        if(!UserAllowedUsingMic) SetMicrophone(VCSettings.MicrophoneDevice);
        interstellarRoom.Rejoin();
        UpdateLocalProfile(true);
        clients.Values.Do(c => c.ResetMapping());
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

    byte myLastId = byte.MaxValue;
    string myLastName = null;
    private void TryUpdateLocalProfile() => UpdateLocalProfile(false);

    private void UpdateLocalProfile(bool always)
    {
        var localPlayer = PlayerControl.LocalPlayer;
        if (!localPlayer) return;

        if (always || localPlayer.PlayerId != myLastId || localPlayer.name != myLastName)
        {
            myLastId = localPlayer.PlayerId;
            myLastName = localPlayer.name;
            interstellarRoom.UpdateProfile(myLastName, myLastId);
        }
    }

    private void UpdateInternal()
    {
#if ANDROID
        PushAudioData();
#endif

        if (LobbyBehaviour.Instance) TryUpdateLocalProfile();
        
        if(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Mute).KeyDown) interstellarRoom.SetMute(!interstellarRoom.Mute);

        if (NebulaGameManager.Instance == null || NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted || NebulaGameManager.Instance.GameState == NebulaGameStates.Finished)
        {
            foreach (var c in clients.Values) c.Update(Vector2.zero, _speakerCache, true);
        }
        else
        {
            float hearDistance = HearDistance;
            var localPlayer = GamePlayer.LocalPlayer;
            var camTarget = HudManager.InstanceExists && HudManager.Instance && HudManager.Instance.PlayerCam ? AmongUsUtil.CurrentCamTarget : null;
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

            var vc = ModSingleton<NoSVCRoom>.Instance;
            if (vc != null)
            {
                vc.UpdateVoiceChatInfo();
                vc.UpdateInternal();
                vc.UpdateRadio();
            }
        }
    }

    static public Image IconRadioImage = SpriteLoader.FromResource("Nebula.Resources.UpperIconRadio.png", 100f);
    private class RadioChannel
    {
        static readonly private IDividedSpriteLoader radioImages = DividedSpriteLoader.FromResource("Nebula.Resources.RadioIcons.png", 100f, 50, 50, true);
        public Color Color { get; }
        public string LocalizedName { get; }
        public Image Image { get; }
        Func<GamePlayer, bool> canHear;
        private readonly ILifespan lifespan;
        public ILifespan Lifespan => lifespan;
        public bool IsDead => lifespan.IsDeadObject;
        public bool CanHear(GamePlayer player) => canHear.Invoke(player);
        public RadioChannel(string localizedName,int imageId, Func<GamePlayer, bool> canHear, ILifespan lifespan, Color color)
        {
            this.LocalizedName = localizedName;
            this.Image = radioImages.AsLoader(imageId);
            this.canHear = canHear;
            this.lifespan = lifespan;
            Color = color;
        }
    }

    public void RegisterRadioChannel(string localizedName, int imageId, Func<GamePlayer, bool> canHear, ILifespan lifespan, Color color)
    {
        var radio = new RadioChannel(localizedName, imageId, canHear, lifespan, color);
        radios.Add(radio);
        RegisterWidget(localizedName, color, radio.Image, lifespan, () => CurrentRadio == radio, 10);
    }

    private List<RadioChannel> radios = [];
    private RadioChannel? CurrentRadio { get; set; }

    private VirtualInput radioInput = NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.VCRadio);
    private void UpdateRadio()
    {
        radios.RemoveAll(r => r.IsDead);
        if (CurrentRadio?.IsDead ?? false) CurrentRadio = null;
        if (MeetingHud.Instance && CurrentRadio != null) CurrentRadio = null;
        
        ulong mask = 0;
        bool radio = CurrentRadio != null;
        if (radio) GamePlayer.AllPlayers.Where(CurrentRadio!.CanHear).Do(p => mask |= 1UL << p.PlayerId);
        UpdateVoiceState(radio, mask);

        if (radioInput.KeyDownInGame && radios.Count > 0 && !MeetingHud.Instance)
        {
            NebulaManager.Instance.ShowRingMenu(
                radios.Select(r => new RingMenu.RingMenuElement(new NoSGUIImage(GUIAlignment.Center, IconRadioImage, new(null, 0.54f))
                {
                    PostBuilder = origRenderer =>
                    {
                        var renderer = UnityHelper.CreateSpriteRenderer("Icon", origRenderer.transform, new(0.18f, 0.12f, -0.01f));
                        renderer.sprite = r.Image.GetSprite();
                        renderer.SetBothOrder(0);
                        origRenderer.SetBothOrder(0);
                    }
                }, () => CurrentRadio = r, GUI.API.RawText(GUIAlignment.Center, AttributeAsset.OverlayContent, r.LocalizedName)))
                .Prepend(new RingMenu.RingMenuElement(new NoSGUIImage(GUIAlignment.Center, DynamicPalette.colorInvalidSprite, new(0.9f,0.9f)), () => CurrentRadio = null, GUI.API.RawText(GUIAlignment.Center, AttributeAsset.OverlayContent, Language.Translate("voiceChat.info.returnToVC")))).ToArray(),
                () => radioInput.KeyInGame, null);
        }
    }
    
    bool __lastRadio = false;
    ulong __lastMask = 0;
        
    private void UpdateVoiceState(bool radio, ulong mask)
    {
        if (__lastRadio != radio || (radio && __lastMask != mask))
        {
            __lastMask = mask;
            __lastRadio = radio;
            RpcUpdateVoiceState.Invoke((PlayerControl.LocalPlayer.PlayerId, radio, mask));
        }
    }

    static private RemoteProcess<(int playerId, bool radio, ulong mask)> RpcUpdateVoiceState = new("VoiceState",
        (message, calledByMe) =>
        {
            if (calledByMe) return;
            var room = ModSingleton<NoSVCRoom>.Instance;
            if (room == null) return;
            if (GamePlayer.LocalPlayer == null) return;
            room.voiceStates[message.playerId] = new(message.radio, !message.radio || (message.mask & (1LU << GamePlayer.LocalPlayer.PlayerId)) != 0);
        });

    internal IMetaWidgetOld UpdateWidget(out bool found)
    {
        if (!UsingMicrophone)
        {
            found = true;
            return noMicWidget;
        }
        WidgetRecord? widget = null;
        widgets.RemoveAll(w =>
        {
            var isDead = w.lifespan.IsDeadObject;
            if (widget == null && !isDead && w.predicate.Invoke()) widget = w;
            return isDead;
        });
        found = widget != null;
        return widget?.widget ?? defaultWidget;
    }

    private IMetaWidgetOld? currentWidget = null;
    private IMetaWidgetOld defaultWidget = GetNormalWidget(Language.Translate("voiceChat.info.unmute"), null);
    private IMetaWidgetOld noMicWidget = GetNormalWidget(Language.Translate("voiceChat.info.nomic"), null);
    private record WidgetRecord(IMetaWidgetOld widget, ILifespan lifespan, Func<bool> predicate, int priority);
    private OrderedList<WidgetRecord, int> widgets = OrderedList<WidgetRecord, int>.DescendingList(v => v.priority);

    static private IMetaWidgetOld GetNormalWidget(string localizedText, Color? color) => new MetaWidgetOld.Text(TextAttribute) { Alignment = IMetaWidgetOld.AlignmentOption.Center, RawText = color.HasValue ? localizedText.Color(color.Value) : localizedText };
    private void RegisterWidget(string localizedText, Color? color, Image? radioIcon, ILifespan lifespan, Func<bool> predicate, int priority)
    {
        MetaWidgetOld GetRadioWidget()
        {
            var widget = new MetaWidgetOld();

            widget.Append(
                new ParallelWidgetOld(
                new(new MetaWidgetOld.Image(NoSVCRoom.IconRadioImage.GetSprite())
                {
                    Width = 0.22f,
                    Alignment = IMetaWidgetOld.AlignmentOption.Center,
                    PostBuilder = renderer =>
                    {
                        var icon = UnityHelper.CreateObject<SpriteRenderer>("icon", renderer.transform, new(0.18f, 0.12f, -0.01f));
                        icon.sprite = radioIcon.GetSprite();
                        icon.SetBothOrder(20);
                    }
                }, 0.35f),
                new(new MetaWidgetOld()
                .Append(new MetaWidgetOld.VerticalMargin(0.015f))
                .Append(new MetaWidgetOld.Text(SmallTextAttribute) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "voiceChat.info.radio" })
                .Append(new MetaWidgetOld.VerticalMargin(-0.07f))
                .Append(new MetaWidgetOld.Text(TextAttribute) { Alignment = IMetaWidgetOld.AlignmentOption.Center, RawText = color.HasValue ? localizedText.Color(color.Value) : localizedText })
                , 1.6f))
                { Alignment = IMetaWidgetOld.AlignmentOption.Center });
            return widget;
        }

        if (radioIcon != null)
        {
            widgets.Add(new(GetRadioWidget(), lifespan, predicate, priority));
        }
        else
        {
            widgets.Add(new(GetNormalWidget(localizedText, color), lifespan, predicate, priority));
        }
    }

    private bool showDebugInfo = false;
    internal void ToggleDebugInfo()
    {
        showDebugInfo = !showDebugInfo;
        if (showDebugInfo)
        {
            DebugScreen.Push(new FunctionalDebugTextContent(() =>
            {
                try
                {
                    string title = "<b>VC Status</b><br>";
                    if (clients.Count == 0) return title + "No players connect to VC else you.";

                    string players = title + string.Join("<br>", clients.Select(entry =>
                    {
                        return $"{entry.Value.PlayerName}({entry.Value.PlayerId}): {(entry.Value.IsMapped ? entry.Value.Level.ToString("F6") + "(" + entry.Value.MappedPlayerControl.name + "," + entry.Value.MappedPlayerControl.PlayerId + ")" : "Unmapped")}";
                    }));
                    var unmapped = PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => !p.AmOwner && !clients.Any(c => c.Value.MappedPlayerControl == p)).ToArray();
                    if (unmapped.Length > 0)
                    {
                        players += "<br><br>Unconnected Players:<br>" + string.Join("<br>", unmapped.Select(p => " -" + p.name));
                    }
                    players += "<br><br> My Status: " + (this.myLastName ?? "NONAME") + ", " + this.myLastId;
                    return players;
                }
                catch
                {
                    return "Error";
                }
            }, new FunctionalLifespan(() => IsActive && showDebugInfo)));
        }
    }

    public static TextAttributeOld TextAttribute { get; private set; } = new(TextAttributeOld.BoldAttr) { Size = new(1.2f, 0.4f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaxSize = 1.8f, FontMinSize = 1f, FontSize = 1.8f };
    public static TextAttributeOld SmallTextAttribute { get; private set; } = new(TextAttributeOld.BoldAttr) { Size = new(1.2f, 0.15f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaxSize = 1.2f, FontMinSize = 0.7f, FontSize = 1.2f };
}

    
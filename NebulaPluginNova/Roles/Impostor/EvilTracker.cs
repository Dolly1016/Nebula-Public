using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Nebula.Roles.Abilities;
using Newtonsoft.Json.Utilities;
using System.Text;
using TMPro;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;


public class TrackerTaskMapLayer : MonoBehaviour
{
    ObjectPool<PooledMapIcon> iconPool = null!;

    static TrackerTaskMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<TrackerTaskMapLayer>();


    public void Awake()
    {
        iconPool = new(ShipStatus.Instance.MapPrefab.taskOverlay.icons.Prefab.GetComponent<PooledMapIcon>(), transform);
        iconPool.OnInstantiated = icon =>
        {
            icon.rend.color = Color.yellow;
            icon.alphaPulse.enabled = false;
        };
    }

    public void SetIcons(Vector2[] locations)
    {
        iconPool.RemoveAll();

        foreach (var location in locations)
        {
            var icon = iconPool.Instantiate();
            Vector3 localPos = location / ShipStatus.Instance.MapScale;
            localPos.z = -1f;
            icon.transform.localPosition = localPos;
        }
    }
}

public class TrackerPlayerMapLayer : MonoBehaviour
{
    ObjectPool<SpriteRenderer> iconPool = null!;
    public GamePlayer? Target = null;

    static TrackerPlayerMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<TrackerPlayerMapLayer>();

    public void Awake()
    {
        iconPool = new(ShipStatus.Instance.MapPrefab.HerePoint, transform);
        iconPool.OnInstantiated = icon => PlayerMaterial.SetColors(Target?.PlayerId ?? 0, icon);
    }

    public void ClearPool()
    {
        iconPool.DestroyAll();
    }
    public void Update()
    {
        if (Target == null) return;
        iconPool.RemoveAll();

        var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
        var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);


        if (!Target!.IsDead && !MeetingHud.Instance)
        {
            var icon = iconPool.Instantiate();
            icon.transform.localPosition = VanillaAsset.ConvertToMinimapPos(Target.Position, center, scale);
        }

    }
}

public class EvilTracker : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private EvilTracker() : base("evilTracker", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [ShowKillFlashOption, TaskTrackingOption, TrackCoolDownOption, CanChangeTargetOption, CanChangeTargetOnMeetingOption, UpdateArrowIntervalOption, TrackImpostorsOption,ShowTrackingTargetOnMapOption, CanCheckTrackingTasksInTaskPhaseOption, ShowWhereTrackingIsOption]) { }
    Citation? HasCitation.Citaion => Citations.TheOtherRolesGMH;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private BoolConfiguration ShowKillFlashOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.showKillFlash", false);
    static private ValueConfiguration<int> TaskTrackingOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.taskTracking", ["options.role.evilTracker.taskTracking.off", "options.role.evilTracker.taskTracking.onlyTarget", "options.role.evilTracker.taskTracking.on"], 0);
    static private FloatConfiguration TrackCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.trackCoolDown", (10f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanChangeTargetOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.canChangeTarget", false);
    static private BoolConfiguration CanChangeTargetOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.canChangeTargetOnMeeting", false, ()=>CanChangeTargetOption && TaskTrackingOption.GetValue() == 2);
    static private FloatConfiguration UpdateArrowIntervalOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.updateArrowInterval", (0f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration TrackImpostorsOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.trackImpostors", false);
    static private BoolConfiguration ShowTrackingTargetOnMapOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.showTrackingTargetOnMap", false);
    static private BoolConfiguration CanCheckTrackingTasksInTaskPhaseOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.canCheckTrackingTasksInTaskPhase", false);
    static private BoolConfiguration ShowWhereTrackingIsOption = NebulaAPI.Configurations.Configuration("options.role.evilTracker.showWhereTrackingIs", false);
    //private NebulaConfiguration ShowRoomWhereTrackingTargetIsOption = null!;

    static public EvilTracker MyRole = new EvilTracker();

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? trackButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.TrackButton.png", 115f);
        static private Image taskButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.TaskTrackButton.png", 115f);


        public Instance(GamePlayer player) : base(player)
        {
        }

        GamePlayer? trackingTarget = null;

        TrackingArrowAbility? arrowAbility = null;

        AchievementToken<(Vector2[]? locs, byte target, bool cleared)>? challengeToken = null;

        List<TrackingArrowAbility> impostorArrows = new();

        private void TryRegisterArrow(GamePlayer player) { if(!impostorArrows.Any(a => a.MyPlayer == player)) impostorArrows.Add(Bind(new TrackingArrowAbility(player.Unbox(), 0f, Palette.ImpostorRed)).Register()); }

        //役職変化に応じて矢印を付ける
        void OnSetRole(PlayerRoleSetEvent ev)
        {
            if (TrackImpostorsOption)
            {
                if (!ev.Player.AmOwner && ev.Player.IsImpostor)
                    TryRegisterArrow(ev.Player);
                else
                {
                    impostorArrows.RemoveAll(a => { if (a.MyPlayer == ev.Player) { a.ReleaseIt(); return true; } else return false; });
                }
            }
        }

        void ChangeTrackingTarget(GamePlayer target)
        {
            trackingTarget = target;

            if (TrackingIcon) GameObject.Destroy(TrackingIcon?.gameObject);
            if (trackingTarget != null)
            {
                TrackingIcon = trackButton!.GeneratePlayerIcon(trackingTarget);

                if (arrowAbility != null) arrowAbility.ReleaseIt();
                arrowAbility = Bind(new TrackingArrowAbility(trackingTarget, UpdateArrowIntervalOption, Color.white)).Register();
            }
        }

        PoolablePlayer? TrackingIcon = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                if (ShowWhereTrackingIsOption)
                {
                    var TextHolder = HudContent.InstantiateContent("TrackingText", true, true, false, false);
                    this.Bind(TextHolder.gameObject);

                    TextMeshPro tmPro = null!;
                    var text = new NoSGUIText(Virial.Media.GUIAlignment.Left, new(GUI.API.GetAttribute(Virial.Text.AttributeParams.StandardBaredBoldLeftNonFlexible)) { Alignment = Virial.Text.TextAlignment.BottomLeft, FontSize = new(1.6f), Size = new(3f, 1f) }, new RawTextComponent("")) { PostBuilder = t => { tmPro = t; tmPro.sortingOrder = 0; } } ;
                    text.Instantiate(new Virial.Media.Anchor(new(0f, 0f), new(-0.5f, -0.5f, 0f)), new(20f, 20f), out _)!.transform.SetParent(TextHolder.transform, false);
                    
                    GameOperatorManager.Instance?.Register<GameUpdateEvent>(ev => {
                        if (tmPro)
                        {
                            StringBuilder text = new();

                            string GetRoomName(GamePlayer player)
                            {
                                foreach (var entry in ShipStatus.Instance.FastRooms) if (entry.value.roomArea.OverlapPoint(player.TruePosition)) return AmongUsUtil.ToDisplayString(entry.Key);
                                return Language.Translate("location.outside");
                            }

                            if (trackingTarget != null && !trackingTarget.IsDead) text.AppendLine((trackingTarget.Name + ": " + GetRoomName(trackingTarget)).Color(Color.Lerp(Palette.PlayerColors[trackingTarget.PlayerId], Color.white, 0.25f)));
                            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.AmOwner && p.IsImpostor && !p.IsDead)) text.AppendLine((p.Name + ": " + GetRoomName(p)).Color(Palette.ImpostorRed));

                            tmPro.text = text.ToString();
                        }
                    }, this);
                }
                //インポスターに矢印を付ける
                if (TrackImpostorsOption) NebulaGameManager.Instance?.AllPlayerInfo().Where(p => !p.AmOwner && p.Role.Role.Category == RoleCategory.ImpostorRole).Do(p => TryRegisterArrow(p));

                var trackTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p)));

                trackButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                trackButton.SetSprite(buttonSprite.GetSprite());
                trackButton.Availability = (button) => trackTracker.CurrentTarget != null && MyPlayer.CanMove;
                trackButton.Visibility = (button) => !MyPlayer.IsDead && (CanChangeTargetOption || trackingTarget == null);
                trackButton.OnClick = (button) =>
                {
                    trackButton.StartCoolDown();
                    ChangeTrackingTarget(trackTracker.CurrentTarget!);
                };
                trackButton.CoolDownTimer = Bind(new Timer(TrackCoolDownOption).SetAsAbilityCoolDown().Start());
                trackButton.SetLabel("track");

                if (CanCheckTrackingTasksInTaskPhaseOption)
                {
                    var mapButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                    mapButton.SetSprite(taskButtonSprite.GetSprite());
                    mapButton.Availability = (button) => MyPlayer.CanMove;
                    mapButton.Visibility = (button) => mapLayer;
                    mapButton.OnClick = (button) =>
                    {
                        MapBehaviour.Instance.ShowNormalMap();
                        MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);
                        mapLayer!.gameObject.SetActive(true);
                    };
                    mapButton.SetLabel("task");
                }

                challengeToken = new("evilTracker.challenge",(null,255,false),(val,_) => val.cleared);
            }
        }

        static private SpriteLoader trackSprite = SpriteLoader.FromResource("Nebula.Resources.TrackIcon.png", 115f);

        TrackerTaskMapLayer? mapLayer = null;
        TrackerPlayerMapLayer? playerMapLayer = null;

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            int optionValue = TaskTrackingOption.GetValue();
            if (optionValue > 0)
            {
                GamePlayer? isChecked = null;
                NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>()?.RegisterMeetingAction(new Behaviour.MeetingPlayerAction(
                    trackSprite,
                    p =>
                    {
                        if (isChecked == null)
                        {
                            RpcShareTaskLoc.Invoke((MyPlayer.PlayerId, p.MyPlayer.PlayerId));
                            isChecked = p.MyPlayer;
                            if(CanChangeTargetOnMeetingOption && CanChangeTargetOption) ChangeTrackingTarget(p.MyPlayer);
                        }
                        else if (mapLayer)
                        {
                            MapBehaviour.Instance.ShowNormalMap();
                            MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);
                            mapLayer!.gameObject.SetActive(true);
                        }
                    },
                    p => !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && (optionValue == 2 || trackingTarget == p.MyPlayer) && (isChecked == null || isChecked == p.MyPlayer)
                    ));
            }
        }

        [Local]
        void OnMeetingEnd(MeetingStartEvent ev)
        {
            if (mapLayer)
            {
                GameObject.Destroy(mapLayer!.gameObject);
                mapLayer = null;
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if(ShowKillFlashOption && !ev.Murderer.AmOwner) AmongUsUtil.PlayQuickFlash(Palette.ImpostorRed);   
        }

        [Local]
        void OnOpenNormalMap(MapOpenNormalEvent ev)
        {
            if (AmOwner && mapLayer) mapLayer!.gameObject.SetActive(false);
            if (playerMapLayer) playerMapLayer!.gameObject.SetActive(false);
        }

        [Local]
        void OnOpenAdminMap(MapOpenAdminEvent ev) {
            if (playerMapLayer) playerMapLayer!.gameObject.SetActive(false);
            if (mapLayer) mapLayer!.gameObject.SetActive(false);
        }

        [Local]
        void OnOpenSabotageMap(MapOpenSabotageEvent ev)
        {
            if (ShowTrackingTargetOnMapOption)
            {
                if (!playerMapLayer)
                {
                    playerMapLayer = UnityHelper.CreateObject<TrackerPlayerMapLayer>("TrackerPlayerLayer", MapBehaviour.Instance.transform, new(0f, 0f, -1f));
                    this.Bind(playerMapLayer.gameObject);
                }

                playerMapLayer!.ClearPool();
                playerMapLayer!.Target = trackingTarget;
                playerMapLayer!.gameObject.SetActive(trackingTarget != null);
            }
            if (mapLayer) mapLayer!.gameObject.SetActive(false);
        }


        [OnlyMyPlayer, Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            //タスク周辺でキルしたらチャレンジ実績達成
            if (challengeToken != null && challengeToken.Value.locs != null && challengeToken.Value.target == ev.Dead.PlayerId)
            {
                challengeToken.Value.cleared |= challengeToken.Value.locs.Any(l => l.Distance(ev.Dead.VanillaPlayer.transform.position) < 3f);
            }
        }

        [Local]
        void OnPlayerDead(PlayerDieEvent ev)
        {
            if (trackingTarget == ev.Player) new StaticAchievementToken("evilTracker.another1");
        }

        static private RemoteProcess<(byte myId, byte targetId)> RpcShareTaskLoc = QueryRPC.Generate<(byte myId, byte targetId), (byte myId, byte targetId, Vector2[] vec)>(
            "ShareTaskLoc",
            q => q.targetId == PlayerControl.LocalPlayer.PlayerId,
            q =>
            {
                List<Vector2> list = new();
                foreach(var t in PlayerControl.LocalPlayer.myTasks.GetFastEnumerator().Where(t => !t.IsComplete && t.HasLocation))
                {
                    foreach (var l in t.Locations) list.Add(l);
                }
                return (q.myId, q.targetId, list.ToArray());
            },
            (message, _) =>
            {
                if(message.myId == PlayerControl.LocalPlayer.PlayerId)
                {
                    HudManager.Instance.InitMap();
                    if (MapBehaviour.Instance.IsOpen) MapBehaviour.Instance.Close();
                    MapBehaviour.Instance.ShowNormalMap();

                    if(NebulaGameManager.Instance?.LocalPlayerInfo.Role is EvilTracker.Instance eTracker)
                    {
                        if (!eTracker.mapLayer)
                        {
                            eTracker.mapLayer = UnityHelper.CreateObject<TrackerTaskMapLayer>("TrackerLayer", MapBehaviour.Instance.taskOverlay.transform.parent, Vector3.zero);
                            eTracker.Bind(eTracker.mapLayer.gameObject);
                        }
                        eTracker.mapLayer!.gameObject.SetActive(true);
                        eTracker.mapLayer!.SetIcons(message.vec);
                        MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);

                        if (eTracker.challengeToken != null)
                        {
                            if (message.vec.Length <= 3)
                            {
                                eTracker.challengeToken.Value.locs = message.vec;
                                eTracker.challengeToken.Value.target = message.targetId;
                            }else
                                eTracker.challengeToken.Value.locs = null;
                        }
                        new StaticAchievementToken("evilTracker.common1");
                    }
                }
            }
            );
    }
}

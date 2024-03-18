using Il2CppInterop.Runtime.Injection;
using Mono.CSharp;
using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;


public class TrackerMapLayer : MonoBehaviour
{
    ObjectPool<PooledMapIcon> iconPool = null!;

    static TrackerMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<TrackerMapLayer>();


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
public class EvilTracker : ConfigurableStandardRole, HasCitation
{
    static public EvilTracker MyRole = new EvilTracker();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "evilTracker";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.TheOtherRolesGMH;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration ShowKillFlashOption = null!;
    private NebulaConfiguration TaskTrackingOption = null!;
    private NebulaConfiguration TrackCoolDownOption = null!;
    private NebulaConfiguration CanChangeTargetOption = null!;
    private NebulaConfiguration UpdateArrowIntervalOption = null!;
    private NebulaConfiguration TrackImpostorsOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        TrackCoolDownOption = new NebulaConfiguration(RoleConfig, "trackCoolDown", null, 10f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        TaskTrackingOption = new NebulaConfiguration(RoleConfig, "taskTracking", null, ["options.role.evilTracker.taskTracking.off", "options.role.evilTracker.taskTracking.onlyTarget", "options.role.evilTracker.taskTracking.on"], 0, 0);
        ShowKillFlashOption = new NebulaConfiguration(RoleConfig, "showKillFlash", null, false, false);
        CanChangeTargetOption = new NebulaConfiguration(RoleConfig, "canChangeTarget", null, false, false);
        UpdateArrowIntervalOption = new NebulaConfiguration(RoleConfig, "updateArrowInterval", null, 0f, 30f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        TrackImpostorsOption = new NebulaConfiguration(RoleConfig, "trackImpostors", null, false, false);
    }

    [NebulaRPCHolder]
    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? trackButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.TrackButton.png", 115f);
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        PlayerModInfo? trackingTarget = null;

        TrackingArrowAbility? arrowAbility = null;

        AchievementToken<(Vector2[]? locs, byte target, bool cleared)>? challengeToken = null;

        List<TrackingArrowAbility> impostorArrows = new();

        private void TryRegisterArrow(GamePlayer player) { if(!impostorArrows.Any(a => a.MyPlayer == player)) impostorArrows.Add(Bind(new TrackingArrowAbility(player.Unbox(), 0f, Palette.ImpostorRed)).Register()); }

        //役職変化に応じて矢印を付ける
        void IGameEntity.OnSetRole(Virial.Game.Player player, Virial.Assignable.RuntimeRole role)
        {
            if (MyRole.TrackImpostorsOption)
            {
                if (!role.MyPlayer.AmOwner && role.Role.Category == RoleCategory.ImpostorRole)
                    TryRegisterArrow(player);
                else
                {
                    impostorArrows.RemoveAll(a => { if (a.MyPlayer == player) { a.ReleaseIt(); return true; } else return false; });
                }
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                //インポスターに矢印を付ける
                if (MyRole.TrackImpostorsOption) NebulaGameManager.Instance?.AllPlayerInfo().Where(p => !p.AmOwner && p.Role.Role.Category == RoleCategory.ImpostorRole).Do(p => TryRegisterArrow(p));

                PoolablePlayer? poolablePlayer = null;
                var trackTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, p => ObjectTrackers.StandardPredicate(p)));

                trackButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                trackButton.SetSprite(buttonSprite.GetSprite());
                trackButton.Availability = (button) => trackTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                trackButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && (MyRole.CanChangeTargetOption || trackingTarget == null);
                trackButton.OnClick = (button) =>
                {
                    trackingTarget = trackTracker.CurrentTarget?.GetModInfo();
                    trackButton.StartCoolDown();

                    
                    if (poolablePlayer) GameObject.Destroy(poolablePlayer?.gameObject);
                    if (trackingTarget != null)
                    {
                        poolablePlayer = trackButton.GeneratePlayerIcon(trackingTarget);

                        if (arrowAbility != null) arrowAbility.ReleaseIt();
                        arrowAbility = Bind(new TrackingArrowAbility(trackingTarget, MyRole.UpdateArrowIntervalOption.GetFloat(), Color.white)).Register();
                    }
                };
                trackButton.CoolDownTimer = Bind(new Timer(MyRole.TrackCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                trackButton.SetLabel("track");

                challengeToken = new("evilTracker.challenge",(null,255,false),(val,_) => val.cleared);
            }
        }

        static private SpriteLoader trackSprite = SpriteLoader.FromResource("Nebula.Resources.TrackIcon.png", 115f);

        TrackerMapLayer? mapLayer = null;

        void IGameEntity.OnMeetingStart()
        {
            if (AmOwner)
            {
                int optionValue = MyRole.TaskTrackingOption.CurrentValue;
                if (optionValue > 0)
                {
                    PlayerModInfo? isChecked = null;
                    NebulaGameManager.Instance?.MeetingPlayerButtonManager.RegisterMeetingAction(new Behaviour.MeetingPlayerAction(
                        trackSprite,
                        p =>
                        {
                            if (isChecked == null)
                            {
                                RpcShareTaskLoc.Invoke((MyPlayer.PlayerId, p.MyPlayer.PlayerId));
                                isChecked = p.MyPlayer;
                            }
                            else if(mapLayer)
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
        }

        void IGameEntity.OnMeetingEnd()
        {
            if (AmOwner && mapLayer)
            {
                GameObject.Destroy(mapLayer!.gameObject);
                mapLayer = null;
            }
        }

        void IGameEntity.OnPlayerMurdered(GamePlayer dead, GamePlayer murderer)
        {
            if(MyRole.ShowKillFlashOption && AmOwner && !murderer.AmOwner) AmongUsUtil.PlayQuickFlash(Palette.ImpostorRed);   
        }

        void IGameEntity.OnOpenNormalMap()
        {
            if (AmOwner && mapLayer) mapLayer!.gameObject.SetActive(false);
        }

        void IGamePlayerEntity.OnKillPlayer(Virial.Game.Player target)
        {
            //タスク周辺でキルしたらチャレンジ実績達成
            if(AmOwner && challengeToken != null && challengeToken.Value.locs != null && challengeToken.Value.target == target.PlayerId)
            {
                challengeToken.Value.cleared |= challengeToken.Value.locs.Any(l => l.Distance(target.VanillaPlayer.transform.position) < 3f);
            }
        }

        void IGameEntity.OnPlayerDead(Virial.Game.Player dead)
        {
            if (AmOwner && trackingTarget == dead) new StaticAchievementToken("evilTracker.another1");
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
                            eTracker.mapLayer = UnityHelper.CreateObject<TrackerMapLayer>("TrackerLayer", MapBehaviour.Instance.taskOverlay.transform.parent, Vector3.zero);
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

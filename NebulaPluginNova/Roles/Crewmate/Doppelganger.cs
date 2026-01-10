using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Map;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Roles.MapLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;
using static Nebula.Roles.Impostor.Disturber;
using static Nebula.Roles.Impostor.Sculptor;

namespace Nebula.Roles.Crewmate;

internal class Doppelganger : DefinedSingleAbilityRoleTemplate<Doppelganger.Ability>, DefinedRole, IAssignableDocument
{

    private Doppelganger() : base("doppelganger", new(191, 103, 215), RoleCategory.CrewmateRole, Crewmate.MyTeam, [SwapCooldownOption, SwapDurationOption, DecoyDetectionRadiusOption])
    {
        GameActionTypes.DoppelgangerAction = new("doppelganger.summon", this, isPlacementAction: true);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0, false));

    static private readonly FloatConfiguration SwapCooldownOption = NebulaAPI.Configurations.Configuration("options.role.doppelganger.swapCooldown", (0f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration SwapDurationOption = NebulaAPI.Configurations.Configuration("options.role.doppelganger.swapDuration", (1f, 5f, 0.5f), 2f, FloatConfigurationDecorator.Second);
    static internal readonly FloatConfiguration DecoyDetectionRadiusOption = NebulaAPI.Configurations.Configuration("options.role.doppelganger.detectionRadius", (0f, 10f, 0.25f), 3f, FloatConfigurationDecorator.Ratio);


    static public readonly Doppelganger MyRole = new();
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    //static private readonly GameStatsEntry StatsBait = NebulaAPI.CreateStatsEntry("stats.bait.bait", GameStatsCategory.Roles, MyRole);
    //static private readonly GameStatsEntry StatsKiller = NebulaAPI.CreateStatsEntry("stats.bait.killer", GameStatsCategory.Roles, MyRole);


    public class DoppelgangerMapLayer : FakePlayerMapLayer
    {
        static DoppelgangerMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<DoppelgangerMapLayer>();

        private Ability myAbility;
        private SpriteRenderer doppelgangerRenderer = null;
        static private readonly Image flagButtonSprite = SpriteLoader.FromResource("Nebula.Resources.DoppelgangerFlag.png", 100f);
        public void InjectAbility(Ability ability) => myAbility = ability;

        private SpriteRenderer flagRenderer = null!;
        protected override void Awake()
        {
            base.Awake();

            flagRenderer = UnityHelper.CreateObject<SpriteRenderer>("FlagRenderer", transform, new(0f, 0f, -20f));
            flagRenderer.sprite = flagButtonSprite.GetSprite();
            flagRenderer.transform.localScale = Vector3.one * 0.65f;
            flagRenderer.enabled = false;

            doppelgangerRenderer = UnityHelper.CreateObject<SpriteRenderer>("DoppelHere", transform, new(0f, 0f, -22f));
            doppelgangerRenderer.sprite = MapBehaviour.Instance.HerePoint.sprite;
            doppelgangerRenderer.material = HatManager.Instance.PlayerMaterial;
            doppelgangerRenderer.material.SetColor(PlayerMaterial.BackColor, DynamicPalette.ShadowColors[PlayerControl.LocalPlayer.PlayerId].RGBMultiplied(0.88f));
            doppelgangerRenderer.material.SetColor(PlayerMaterial.BodyColor, DynamicPalette.PlayerColors[PlayerControl.LocalPlayer.PlayerId].RGBMultiplied(0.88f));
            doppelgangerRenderer.material.SetColor(PlayerMaterial.VisorColor, DynamicPalette.VisorColors[PlayerControl.LocalPlayer.PlayerId].RGBMultiplied(0.92f));
        }

        protected override void OnClick(Vector2 worldPos, Vector2 minimapPos)
        {
            myAbility.Command(worldPos, () =>
            {
                flagRenderer.enabled = true;
                flagRenderer.transform.localPosition = minimapPos.AsVector3(-20f);
            });
        }
        protected override void Update()
        {
            base.Update();

            if (flagRenderer && (!myAbility.HasFakePlayerLocal || !myAbility.IsMovingLocal)) flagRenderer.enabled = false;

            doppelgangerRenderer.enabled = myAbility.HasFakePlayerLocal;
            Vector2 gangerPos = VanillaAsset.ConvertToMinimapPos(myAbility.FakePlayerPosition ?? new(0f, 0f), CurrentMapCenter, CurrentMapScale);
            doppelgangerRenderer.transform.localPosition = gangerPos.AsVector3(-15f);
        }
    }

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(mapButtonSprite, "role.doppelganger.ability.map");
        yield return new(swapButtonSprite, "role.doppelganger.ability.swap");
        yield return new(despawnButtonSprite, "role.doppelganger.ability.despawn");
    }

    static private Image mapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyMapCrewmateButton.png", 115f);
    static private Image swapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DoppelgangerSwapButton.png", 115f);
    static private Image despawnButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DoppelgangerDespawnButton.png", 115f);

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        //Localでのみ
        private FakePlayer? fakePlayer = null;
        private IReleasable? fakePlayerReleasable = null;
        private bool isMoving = false;
        
        int currentWalkCommandId = 0;
        private ModAbilityButton? swapButton = null;
        public bool HasFakePlayerLocal => fakePlayer != null;
        public Vector2? FakePlayerPosition => fakePlayer?.Position;
        public bool IsMovingLocal => isMoving;
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var mapButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.None, null,
                   0f, "doppelganger.command", mapButtonSprite).SetAsUsurpableButton(this);
                mapButton.OnClick = (button) =>
                {
                    HudManager.Instance.ToggleMapVisible(new MapOptions() { AllowMovementWhileMapOpen = true, Mode = MapOptions.Modes.Normal, ShowLivePlayerPosition = true });
                };

                swapButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "doppelganger.swap",
                    SwapCooldownOption, SwapDurationOption, "swap", swapButtonSprite,
                    _ => !(fakePlayer?.UsingSomeUtility ?? false), _ => fakePlayer != null).SetAsUsurpableButton(this);
                swapButton.OnEffectStart = (button) =>
                {
                    MyPlayer.GainSpeedAttribute(0f, 2f, false, 0);
                    currentWalkCommandId++; //現在の移動を中断する
                };
                swapButton.OnEffectEnd = (button) =>
                {
                    if (fakePlayer != null)
                    {
                        if (MyPlayer.Position.Distance(fakePlayer!.Position) > 20f) new StaticAchievementToken("doppelganger.common1");
                        using (RPCRouter.CreateSection("DoppelgangerSwap"))
                        {
                            RpcDoppelSwap.Invoke((MyPlayer, fakePlayer));
                            SummonDoppelganger(MyPlayer.Position, MyPlayer.VanillaCosmetics.FlipX);
                        }
                    }
                };
                

                var despawnButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "doppelganger.despawn",
                    0f, "despawn", despawnButtonSprite,
                    _ => !(fakePlayer?.UsingSomeUtility ?? false), _ => fakePlayer != null).SetAsUsurpableButton(this);
                despawnButton.OnClick = (button) =>
                {
                    DespawnDoppelganger(true);
                    button.StartCoolDown();
                };
                despawnButton.OnBroken = _ => DespawnDoppelganger(true);
            }
        }
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        internal void DespawnDoppelganger(bool withEffect = false)
        {
            using (RPCRouter.CreateSection("DoppelgangerSwap"))
            {
                if (fakePlayerReleasable != null)
                {
                    if (withEffect) RpcPlayerDespawnEffect.Invoke(fakePlayer!);
                    fakePlayerReleasable.Release();
                }
                fakePlayer = null;
                fakePlayerReleasable = null;
            }

            
        }
        internal void SummonDoppelganger(Vector2 position, bool flipX)
        {
            DespawnDoppelganger(false);

            var lifespan = new FlexibleLifespan();
            lifespan.Bind(this);
            fakePlayerReleasable = lifespan;

            fakePlayer = FakePlayerController.SpawnSyncFakePlayer(MyPlayer, new(position, KillCharacteristics.KillAllAndLeaveBodyOne, true, flipX, MyPlayer.VanillaCosmetics.GetPetPosition())).BindLifespan(lifespan);
            isMoving = false;
        }

        internal bool Command(Vector2 to, Action? onStartWalking = null)
        {
            if (fakePlayer == null) SummonDoppelganger(MyPlayer.Position, MyPlayer.VanillaCosmetics.FlipX);
            if (IsUsurped) return false;
            if (swapButton?.IsInEffect ?? false) return false; //スワップ待ちの場合は移動を命令できない。

            var path = NavVerticesHelpers.CalcPath(fakePlayer!.TruePosition, to);
            if (path == null)
            {
                return false;
            }
            else
            {
                var fakePlayerCache = fakePlayer;
                int myWalkId = ++currentWalkCommandId;

                ManagedEffects.Sequence(
                    ManagedEffects.Wait(() => isMoving, () =>
                    {
                        isMoving = true;
                        onStartWalking?.Invoke();
                    }),
                    NavVerticesHelpers.WalkPath(path.Path, path.StopCond, fakePlayer.Logics, () => currentWalkCommandId > myWalkId),
                    ManagedEffects.Action(() =>
                    {
                        if (fakePlayer == fakePlayerCache)
                        {
                            isMoving = false;
                            if (currentWalkCommandId == myWalkId) AmongUsUtil.PlayCustomFlash(MyRole.UnityColor, 0f, 0.5f, 0.35f, 0.25f);
                        }

                    })
                ).StartOnScene();
                NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.DoppelgangerAction);
                return true;
            }
        }

        DoppelgangerMapLayer? mapLayer = null;

        [Local]
        void OnInteract(PlayerInteractionFailedForMyFakePlayerEvent ev)
        {
            if (fakePlayer != null && ev.Target == fakePlayer) DespawnDoppelganger(true);
        }

        void OnTryMurder(PlayerKillFakePlayerEvent ev)
        {
            if (fakePlayer != null && ev.Target == fakePlayer)
            {
                if(AmOwner) new StaticAchievementToken("doppelganger.another1");
                ManagedEffects.CoDisappearEffect(LayerExpansion.GetPlayersLayer(), null, ev.Target.RealPlayer.Position.ToUnityVector().AsVector3(-1f))
                    .StartOnScene();
            }
        }

        [Local]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (MyPlayer.Tasks.IsCompletedTotalTasks && MyPlayer.Tasks.TotalTasks > 0)
            {
                if (GamePlayer.AllPlayers.All(p => p.AmOwner || !p.Tasks.IsCrewmateTask || !p.Tasks.HasExecutableTasks || p.Tasks.TotalCompleted + 4 <= p.Tasks.Quota))
                    new StaticAchievementToken("doppelganger.challenge");
            }
        }


        [Local]
        void OnOpenMap(AbstractMapOpenEvent ev)
        {
            if (ev is MapOpenNormalEvent && !IsUsurped && !MeetingHud.Instance && !MyPlayer.IsDead)
            {
                if (!mapLayer)
                {
                    mapLayer = UnityHelper.CreateObject<DoppelgangerMapLayer>("DoppelLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    mapLayer.gameObject.AddComponent<ShowPlayersMapLayer>().SetUp(p => (fakePlayer?.IsActive ?? false) && !p.AmOwner && p.Position.Distance(fakePlayer.Position) < DecoyDetectionRadiusOption, null);
                    this.BindGameObject(mapLayer.gameObject);
                    mapLayer.InjectAbility(this);
                }
                mapLayer!.gameObject.SetActive(true);
            }
            else
            {
                if (mapLayer) mapLayer!.gameObject.SetActive(false);
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => DespawnDoppelganger();

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev) => DespawnDoppelganger(true);


        [OnlyMyPlayer, Local]
        void OnUsurped(PlayerUsurpedAbilityEvent ev) => DespawnDoppelganger(true);

        void IGameOperator.OnReleased()
        {
            DespawnDoppelganger(true);
        }
        

        static private RemoteProcess<(GamePlayer player, IPlayerlike doppelganger)> RpcDoppelSwap = new("DoppelSwap",
            (message, _) =>
            {
                var doppelPos = message.doppelganger.Position;
                var doppelPetPos = message.doppelganger.VanillaCosmetics.GetPetPosition();
                
                var flipX = message.doppelganger.VanillaCosmetics.FlipX;

                message.player.VanillaPlayer.transform.position = doppelPos.ToUnityVector().AsVector3(doppelPos.y / 1000f);
                message.player.VanillaPlayer.NetTransform.Halt();
                message.player.VanillaPlayer.cosmetics.SetFlipX(flipX);
                message.player.VanillaPlayer.cosmetics.SetPetPosition(doppelPetPos ?? doppelPos);
            }
            );

        static internal RemoteProcess<IPlayerlike> RpcPlayerDespawnEffect = new("DoppelDespawn",
            (message, _) =>
            {
                ManagedEffects.CoDisappearEffect(LayerExpansion.GetPlayersLayer(), null, message.Position.ToUnityVector().AsVector3(-1f), 1f)
                    .StartOnScene();
            }
            );
    }
}
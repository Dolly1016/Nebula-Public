using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Behavior;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Marionette : DefinedSingleAbilityRoleTemplate<Marionette.Ability>, DefinedRole
{
    private Marionette() : base("marionette", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [MannequinModeOption, FixDecoyAppearanceOption, PlaceCoolDownOption, SwapCoolDownOption, DecoyDurationOption, CanSeeDecoyInShadowOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);

        GameActionTypes.DecoyPlacementAction = new("marionette.placement", this, isEquippingAction: true);
    }


    static private FloatConfiguration PlaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.marionette.placeCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration SwapCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.marionette.swapCoolDown", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DecoyDurationOption = NebulaAPI.Configurations.Configuration("options.role.marionette.decoyDuration", (5f, 180f, 5f), 40f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanSeeDecoyInShadowOption = NebulaAPI.Configurations.Configuration("options.role.marionette.canSeeDecoyInShadow", false);
    static private ValueConfiguration<int> MannequinModeOption = NebulaAPI.Configurations.Configuration("options.role.marionette.mannequinMode", ["options.role.marionette.mannequinMode.mannequin", "options.role.marionette.mannequinMode.player"], 0);
    static private BoolConfiguration FixDecoyAppearanceOption = NebulaAPI.Configurations.Configuration("options.role.marionette.fixDecoyAppearance", false, () => MannequinModeOption.GetValue() == 1);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;

    static public Marionette MyRole = new Marionette();
    static private GameStatsEntry StatsDecoy = NebulaAPI.CreateStatsEntry("stats.marionette.decoy", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsSwap = NebulaAPI.CreateStatsEntry("stats.marionette.swap", GameStatsCategory.Roles, MyRole);

    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Decoy : NebulaSyncStandardObject
    {
        public static string MyTag = "Decoy";
        private static SpriteLoader decoySprite = SpriteLoader.FromResource("Nebula.Resources.Decoy.png", 150f);
        public Decoy(Vector2 pos,bool reverse) : base(pos,ZOption.Just,CanSeeDecoyInShadowOption, decoySprite.GetSprite()) {
            MyRenderer.flipX = reverse;
            MyBehaviour = MyRenderer.gameObject.AddComponent<EmptyBehaviour>();
        }

        public bool Flipped { get => MyRenderer.flipX; set => MyRenderer.flipX = value; }
        public EmptyBehaviour MyBehaviour = null!;

        static Decoy()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new Decoy(new Vector2(args[0], args[1]), args[2] < 0f));
        }

        public override void OnReleased()
        {
            base.OnReleased();
            
            try
            {
                NebulaManager.Instance.StartCoroutine(ManagedEffects.CoDisappearEffect(LayerExpansion.GetObjectsLayer(), null, Position.AsVector3(-1f)));
            }
            catch { }
        }

    }

    private interface IDecoyController
    {
        Decoy? Decoy { get; }
        IFakePlayer? FakePlayer { get; }
        Vector2 DecoyPosition => Decoy?.Position ?? FakePlayer?.Position ?? Vector2.zero;
        int DecoyId => (MannequinMode ? Decoy?.ObjectId : FakePlayer?.PlayerlikeId) ?? 0;
        MonoBehaviour CamBehaviour => (MannequinMode ? Decoy?.MyBehaviour : FakePlayer?.VanillaCosmetics)!;
        bool DecoyIsInactive => MannequinMode ? Decoy == null : (FakePlayer == null || !FakePlayer.IsActive);
        bool DecoyIsActive => !DecoyIsInactive;
        bool DecoyIsFlipped
        {
            get
            {
                if (DecoyIsActive) return (MannequinMode ? Decoy?.Flipped : FakePlayer?.VanillaCosmetics.FlipX) ?? false;
                return false;
            }
        }
        void DecoySnapTo(Vector2 pos, bool flipped)
        {
            if (Decoy != null)
            {
                Decoy.Position = pos;
                Decoy.Flipped = flipped;
            }
            if (FakePlayer != null)
            {
                FakePlayer.VanillaCosmetics.SetFlipX(flipped);
                FakePlayer.Logic.SnapTo(pos);
            }
        }
    }

    private class DecoyController : IDecoyController
    {
        Decoy myDecoy = null!;
        IFakePlayer myFakePlayer = null!;
        Decoy? IDecoyController.Decoy => myDecoy;
        IFakePlayer? IDecoyController.FakePlayer => myFakePlayer;
        
        public DecoyController(int id)
        {
            if (MannequinMode)
            {
                myDecoy = GetMannequinDecoyById(id)!;
            }
            else
            {
                myFakePlayer = GetFakePlayerById(id)!;
            }

        }

    }

    static internal bool MannequinMode => MannequinModeOption.GetValue() == 0;
    static internal Decoy? GetMannequinDecoyById(int id) => NebulaSyncObject.GetObject<Decoy>(id);
    static internal IFakePlayer? GetFakePlayerById(int id) => IPlayerlike.GetPlayerlike(id) as IFakePlayer;
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IDecoyController
    {
        static private Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyButton.png", 115f);
        static private Image destroyButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyDestroyButton.png", 115f);
        static private Image swapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoySwapButton.png", 115f);
        static private Image monitorButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyMonitorButton.png", 115f);
        static private Image decoyArrowSprite = SpriteLoader.FromResource("Nebula.Resources.DecoyArrow.png", 180f);

        private Decoy? myDecoy = null;
        private IFakePlayer? myFakePlayer = null;
        Decoy? IDecoyController.Decoy => myDecoy;
        IFakePlayer? IDecoyController.FakePlayer => myFakePlayer;

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<(bool cleared, float swapTime)>? acTokenCommon2 = null;
        AchievementToken<(bool cleared, float killTime)>? acTokenChallenge = null;
        Arrow? decoyArrow = null;

        //デコイの撤去
        void IGameOperator.OnReleased()
        {
            DestroyDecoy();
        }

        void DestroyDecoy()
        {
            if (MannequinMode)
            {
                if (myDecoy != null) NebulaSyncObject.RpcDestroy(myDecoy!.ObjectId);
                myDecoy = null;
            }
            else
            {
                if (myFakePlayer != null)
                {
                    if (myFakePlayer.IsActive)
                    {
                        ManagedEffects.RpcDisappearEffect.Invoke((myFakePlayer.Position.AsVector3(-1f), LayerExpansion.GetPlayersLayer()));
                        myFakePlayer.Release();
                    }
                }
                myFakePlayer = null;
            }
        }

        void SpawnDecoy()
        {
            if (MannequinMode)
            {
                myDecoy = (NebulaSyncObject.RpcInstantiate(Decoy.MyTag, [
                           PlayerControl.LocalPlayer.transform.localPosition.x,
                        PlayerControl.LocalPlayer.transform.localPosition.y,
                        PlayerControl.LocalPlayer.cosmetics.FlipX ? -1f : 1f
                           ]).SyncObject as Decoy);
            }
            else
            {
                myFakePlayer = FakePlayerController.SpawnSyncFakePlayer(MyPlayer, new(MyPlayer.Position, KillCharacteristics.Disappear, true, MyPlayer.VanillaCosmetics.FlipX, null, FixDecoyAppearanceOption ? new OutfitCandidate(MyPlayer.CurrentOutfit, "marionette", OutfitPriority.FakeSpecialOutfit, true) : null)).BindLifespan(this);
            }
        }

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                decoyArrow = new Arrow(decoyArrowSprite.GetSprite(), false, true) { IsAffectedByComms = false, FixedAngle = true }.Register(this);
                decoyArrow.IsActive = false;

                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("marionette.another1");
                acTokenCommon2 = new("marionette.common2", (false,-100f),(val,_) => val.cleared);
                acTokenChallenge = new("marionette.challenge", (false,-100f),(val,_)=>val.cleared);

                ModAbilityButton destroyButton = null!, swapButton = null!, monitorButton = null!;

                var placeButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "marionette.place",
                    PlaceCoolDownOption, "place", placeButtonSprite,
                    null, _ => (this as IDecoyController).DecoyIsInactive).SetAsUsurpableButton(this);
                placeButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.DecoyPlacementAction);

                        SpawnDecoy();

                        destroyButton.InterruptEffect();
                        destroyButton.StartEffect();
                    });
                    placeButton.StartCoolDown();
                    swapButton?.StartCoolDown();
                    acTokenCommon ??= new("marionette.common1");
                    StatsDecoy.Progress();
                };

                destroyButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "marionette.destroy",
                    0f, DecoyDurationOption, "destroy", destroyButtonSprite, null, _ => (this as IDecoyController).DecoyIsActive).SetAsUsurpableButton(this);
                destroyButton.OnClick = (button) => destroyButton.InterruptEffect();
                destroyButton.OnEffectEnd = (button) =>
                {
                    DestroyDecoy();
                    placeButton.StartCoolDown();
                };

                swapButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "marionette.swap",
                    SwapCoolDownOption, "swap", swapButtonSprite,
                    null, _ => (this as IDecoyController).DecoyIsActive).BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "marionette.switch").SetAsUsurpableButton(this);
                swapButton.Availability = (button) => (MyPlayer.CanMove || HudManager.Instance.PlayerCam.Target == (this as IDecoyController).CamBehaviour) && (!MyPlayer.VanillaPlayer.inVent && !MyPlayer.VanillaPlayer.onLadder && !MyPlayer.VanillaPlayer.inMovingPlat && !MyPlayer.IsBlown);
                swapButton.OnClick = (button) =>
                {
                    DecoySwap.Invoke((MyPlayer.PlayerId, (this as IDecoyController).DecoyId));
                    button.StartCoolDown();
                    AmongUsUtil.SetCamTarget();

                    float currentTime = NebulaGameManager.Instance!.CurrentTime;
                    acTokenCommon2.Value.cleared |= currentTime - acTokenCommon2.Value.swapTime < 10f;
                    acTokenCommon2.Value.swapTime = currentTime;
                    acTokenAnother!.Value.triggered = true;
                    if (currentTime - acTokenChallenge.Value.killTime < 1f && MyPlayer.VanillaPlayer.GetTruePosition().Distance((this as IDecoyController).DecoyPosition) > 30f)
                        acTokenChallenge.Value.cleared = true;

                    StatsSwap.Progress();
                };
                swapButton.SetLabel("swap");
                var cooldownTimer = swapButton.CoolDownTimer as TimerImpl;
                var lastPredicate = cooldownTimer!.Predicate;
                cooldownTimer.SetPredicate(() => lastPredicate!.Invoke() || HudManager.Instance.PlayerCam.Target == (this as IDecoyController).CamBehaviour);
                

                monitorButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.None,
                    0f, "monitor", monitorButtonSprite, null, _ => (this as IDecoyController).DecoyIsActive).SetAsUsurpableButton(this);
                monitorButton.Availability = (button) => true; //通常のボタンより使用できる条件が緩い
                monitorButton.OnClick = (button) => AmongUsUtil.ToggleCamTarget((this as IDecoyController).CamBehaviour, null);
                monitorButton.OnBroken = (button) => AmongUsUtil.SetCamTarget(null);

                swapButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        swapButton.ResetKeyBinding();
                        monitorButton.BindKey(Virial.Compat.VirtualKeyInput.SecondaryAbility, "marionette.monitor");
                        monitorButton.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "marionette.switch");
                    });
                };
                monitorButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        monitorButton.ResetKeyBinding();
                        swapButton!.BindKey(Virial.Compat.VirtualKeyInput.SecondaryAbility, "marionette.swap");
                        swapButton!.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "marionette.switch");
                    });
                };


                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev => {
                    DestroyDecoy();
                    monitorButton.DoSubClick();
                }, this);
            }
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            if(AmOwner && decoyArrow != null)
            {
                if ((this as IDecoyController).DecoyIsInactive)
                    decoyArrow.IsActive = false;
                else
                {
                    decoyArrow.IsActive = true;
                    decoyArrow.TargetPos = (this as IDecoyController).DecoyPosition;
                }
            }
        }

        [OnlyMyPlayer]
        [Local]
        void OnDead(PlayerDieEvent ev)
        {
            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }

        [OnlyMyPlayer]
        [Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (AmOwner && acTokenChallenge != null) acTokenChallenge.Value.killTime = NebulaGameManager.Instance!.CurrentTime;
        }

    }

    static private RemoteProcess<(byte playerId, int objId)> DecoySwap = new("DecoySwap",
        (message, _) => {
            var marionette = Helpers.GetPlayer(message.playerId);
            IDecoyController decoy = new DecoyController(message.objId);
            
            if (marionette == null || decoy == null) return;
            var marionettePos = marionette.transform.localPosition;
            var marionetteFilp = marionette.cosmetics.FlipX;
            marionette.transform.localPosition = decoy.DecoyPosition;
            decoy.DecoySnapTo(marionettePos, marionetteFilp);
            marionette.NetTransform.Halt();
            marionette.cosmetics.SetFlipX(decoy.DecoyIsFlipped);
        }
        );
}


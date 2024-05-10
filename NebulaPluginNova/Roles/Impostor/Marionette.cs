using Nebula.Behaviour;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Marionette : ConfigurableStandardRole
{
    static public Marionette MyRole = new Marionette();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "marionette";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration PlaceCoolDownOption = null!;
    private NebulaConfiguration SwapCoolDownOption = null!;
    private NebulaConfiguration DecoyDurationOption = null!;
    private NebulaConfiguration CanSeeDecoyInShadowOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagFunny, ConfigurationHolder.TagDifficult);

        PlaceCoolDownOption = new(RoleConfig, "placeCoolDown", null, 5f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        SwapCoolDownOption = new(RoleConfig, "swapCoolDown", null, 2.5f, 60f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        DecoyDurationOption = new(RoleConfig, "decoyDuration", null, 5f, 180f, 5f, 40f, 40f) { Decorator = NebulaConfiguration.SecDecorator };
        CanSeeDecoyInShadowOption = new(RoleConfig, "canSeeDecoyInShadow", null, false, false);
    }

    [NebulaPreLoad]
    public class Decoy : NebulaSyncStandardObject
    {
        public static string MyTag = "Decoy";
        private static SpriteLoader decoySprite = SpriteLoader.FromResource("Nebula.Resources.Decoy.png", 150f);
        public Decoy(Vector2 pos,bool reverse) : base(pos,ZOption.Just,MyRole.CanSeeDecoyInShadowOption, decoySprite.GetSprite()) {
            MyRenderer.flipX = reverse;
            MyBehaviour = MyRenderer.gameObject.AddComponent<EmptyBehaviour>();
        }

        public bool Flipped { get => MyRenderer.flipX; set => MyRenderer.flipX = value; }
        public EmptyBehaviour MyBehaviour = null!;

        public static void Load()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new Decoy(new Vector2(args[0], args[1]), args[2] < 0f));
        }
    }

    public class Instance : Impostor.Instance, IBindPlayer
    {
        private ModAbilityButton? placeButton = null;
        private ModAbilityButton? destroyButton = null;
        private ModAbilityButton? swapButton = null;
        private ModAbilityButton? monitorButton = null;

        static private ISpriteLoader placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyButton.png", 115f);
        static private ISpriteLoader destroyButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyDestroyButton.png", 115f);
        static private ISpriteLoader swapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoySwapButton.png", 115f);
        static private ISpriteLoader monitorButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyMonitorButton.png", 115f);
        public override AbstractRole Role => MyRole;
        public Decoy? MyDecoy = null;
        public Instance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<(bool cleared, float swapTime)>? acTokenCommon2 = null;
        AchievementToken<(bool cleared, float killTime)>? acTokenChallenge = null;
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("marionette.another1");
                acTokenCommon2 = new("marionette.common2", (false,-100f),(val,_) => val.cleared);
                acTokenChallenge = new("marionette.challenge", (false,-100f),(val,_)=>val.cleared);

                placeButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                placeButton.SetSprite(placeButtonSprite.GetSprite());
                placeButton.Availability = (button) => MyPlayer.CanMove;
                placeButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy == null;
                placeButton.CoolDownTimer = Bind(new Timer(MyRole.PlaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                placeButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        MyDecoy = (NebulaSyncObject.RpcInstantiate(Decoy.MyTag, new float[] {
                        PlayerControl.LocalPlayer.transform.localPosition.x,
                        PlayerControl.LocalPlayer.transform.localPosition.y,
                        PlayerControl.LocalPlayer.cosmetics.FlipX ? -1f : 1f }) as Decoy);

                        destroyButton!.ActivateEffect();
                        destroyButton.EffectTimer?.Start();
                    });
                    placeButton.StartCoolDown();
                    swapButton?.StartCoolDown();
                    acTokenCommon ??= new("marionette.common1");
                };
                placeButton.SetLabel("place");

                destroyButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                destroyButton.SetSprite(destroyButtonSprite.GetSprite());
                destroyButton.Availability = (button) => MyPlayer.CanMove;
                destroyButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy != null;
                destroyButton.EffectTimer = Bind(new Timer(MyRole.DecoyDurationOption.GetFloat()));
                destroyButton.OnClick = (button) =>
                {
                    destroyButton.InactivateEffect();
                };
                destroyButton.OnEffectEnd = (button) =>
                {
                    if (MyDecoy != null) NebulaSyncObject.RpcDestroy(MyDecoy!.ObjectId);
                    MyDecoy = null;

                    placeButton.StartCoolDown();
                };
                destroyButton.SetLabel("destroy");

                swapButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility).SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                swapButton.SetSprite(swapButtonSprite.GetSprite());
                swapButton.Availability = (button) => (MyPlayer.CanMove || HudManager.Instance.PlayerCam.Target == MyDecoy?.MyBehaviour) && (!MyPlayer.VanillaPlayer.inVent && !MyPlayer.VanillaPlayer.onLadder && !MyPlayer.VanillaPlayer.inMovingPlat);
                swapButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy != null;
                swapButton.CoolDownTimer = Bind(new Timer(MyRole.SwapCoolDownOption.GetFloat()));
                swapButton.OnClick = (button) =>
                {
                    DecoySwap.Invoke((MyPlayer.PlayerId, MyDecoy!.ObjectId));
                    button.StartCoolDown();
                    AmongUsUtil.SetCamTarget();

                    float currentTime = NebulaGameManager.Instance!.CurrentTime;
                    acTokenCommon2.Value.cleared |= currentTime - acTokenCommon2.Value.swapTime < 10f;
                    acTokenCommon2.Value.swapTime = currentTime;
                    acTokenAnother!.Value.triggered = true;
                    if (currentTime - acTokenChallenge.Value.killTime < 1f && MyPlayer.VanillaPlayer.GetTruePosition().Distance(MyDecoy!.Position) > 30f)
                        acTokenChallenge.Value.cleared = true;
                };
                swapButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        swapButton.ResetKeyBind();
                        monitorButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                        monitorButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                    });
                };
                swapButton.SetLabel("swap");

                monitorButton = Bind(new ModAbilityButton());
                monitorButton.SetSprite(monitorButtonSprite.GetSprite());
                monitorButton.Availability = (button) => true;
                monitorButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy != null;
                monitorButton.OnClick = (button) =>
                {
                    AmongUsUtil.ToggleCamTarget(MyDecoy!.MyBehaviour, null);
                };
                monitorButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        monitorButton.ResetKeyBind();
                        swapButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                        swapButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                    });
                };
                monitorButton.SetLabel("monitor");
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
                if (MyDecoy != null) NebulaSyncObject.RpcDestroy(MyDecoy!.ObjectId);
                MyDecoy = null;

                monitorButton?.DoSubClick();
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
            var decoy = NebulaSyncObject.GetObject<Decoy>(message.objId);
            if (marionette == null || decoy == null) return;
            var marionettePos = marionette.transform.localPosition;
            var marionetteFilp = marionette.cosmetics.FlipX;
            marionette.transform.localPosition = decoy.Position;
            marionette.cosmetics.SetFlipX(decoy.Flipped);
            decoy.Position = marionettePos;
            decoy.Flipped = marionetteFilp;
            marionette.NetTransform.Halt();
        }
        );
}


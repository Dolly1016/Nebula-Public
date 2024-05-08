using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class Illusioner : ConfigurableStandardRole
{
    static public Illusioner MyRole = new Illusioner();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "illusioner";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[]? arguments) => new Instance(player);

    private NebulaConfiguration SampleCoolDownOption = null!;
    private NebulaConfiguration MorphCoolDownOption = null!;
    private NebulaConfiguration MorphDurationOption = null!;
    private NebulaConfiguration PaintCoolDownOption = null!;
    private NebulaConfiguration LoseSampleOnMeetingOption = null!;
    private NebulaConfiguration TransformAfterMeetingOption = null!;
    private NebulaConfiguration SampleOriginalLookOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagChaotic, ConfigurationHolder.TagDifficult);

        SampleCoolDownOption = new NebulaConfiguration(RoleConfig, "sampleCoolDown", null, 0f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        MorphCoolDownOption = new NebulaConfiguration(RoleConfig, "morphCoolDown", null, 0f, 60f, 5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        MorphDurationOption = new NebulaConfiguration(RoleConfig, "morphDuration", null, 5f, 120f, 2.5f, 25f, 25f) { Decorator = NebulaConfiguration.SecDecorator };
        PaintCoolDownOption = new NebulaConfiguration(RoleConfig, "paintCoolDown", null, 0f, 60f, 5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        LoseSampleOnMeetingOption = new NebulaConfiguration(RoleConfig, "loseSampleOnMeeting", null, false, false);
        TransformAfterMeetingOption = new NebulaConfiguration(RoleConfig, "transformAfterMeeting", null, false, false);
        SampleOriginalLookOption = new NebulaConfiguration(RoleConfig, "sampleOriginalLook", null, false, false);
    }

    public class Instance : Impostor.Instance
    {
        private ModAbilityButton? sampleButton = null;
        private ModAbilityButton? morphButton = null;
        private ModAbilityButton? paintButton = null;

        StaticAchievementToken? acTokenMorphingCommon = null, acTokenPainterCommon = null, acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;

        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenChallenge = new("illusioner.challenge", 0, (val, _) =>
                {
                    return
                    NebulaGameManager.Instance!.AllPlayerInfo().Where(p => p.PlayerState == PlayerState.Exiled && (val & (1 << p.PlayerId)) != 0).Count() > 0 &&
                    NebulaGameManager.Instance!.AllPlayerInfo().Where(p => (p.MyKiller?.AmOwner ?? false) && (val & (1 << p.PlayerId)) != 0).Count() > 0;
                });

                GameData.PlayerOutfit? sample = null;
                PoolablePlayer? sampleIcon = null;
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                sampleButton.SetSprite(Morphing.Instance.SampleButtonSprite.GetSprite());
                sampleButton.Availability = (button) => MyPlayer.CanMove;
                sampleButton.Visibility = (button) => !MyPlayer.IsDead;
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker.CurrentTarget?.Unbox().GetOutfit(MyRole.SampleOriginalLookOption ? 35 : 75) ?? null;
                    if (sample != null) acTokenChallenge.Value |= 1 << sample.ColorId;

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample, sampleButton.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                };
                sampleButton.CoolDownTimer = Bind(new Timer(MyRole.SampleCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("sample");

                morphButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility).SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                morphButton.SetSprite(Morphing.Instance.MorphButtonSprite.GetSprite());
                morphButton.Availability = (button) => MyPlayer.CanMove && sample != null;
                morphButton.Visibility = (button) => !MyPlayer.IsDead;
                morphButton.OnClick = (button) => {
                    button.ToggleEffect();
                };
                morphButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        morphButton.ResetKeyBind();
                        paintButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                        paintButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                    });
                };
                morphButton.OnEffectStart = (button) =>
                {
                    PlayerModInfo.RpcAddOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, new("Morphing", 50, true, sample!)));

                    acTokenMorphingCommon ??= new("morphing.common1");
                    if (acTokenPainterCommon != null) acTokenCommon ??= new("illusioner.common1");
                };
                morphButton.OnEffectEnd = (button) =>
                {
                    PlayerModInfo.RpcRemoveOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, "Morphing"));
                    morphButton.CoolDownTimer?.Start();
                };
                morphButton.OnMeeting = (button) =>
                {
                    morphButton.InactivateEffect();

                    if (MyRole.LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                        sample = null;
                    }
                };
                morphButton.CoolDownTimer = Bind(new Timer(MyRole.MorphCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                morphButton.EffectTimer = Bind(new Timer(MyRole.MorphDurationOption.GetFloat()));
                morphButton.SetLabel("morph");

                paintButton = Bind(new ModAbilityButton());
                paintButton.SetSprite(Morphing.Instance.MorphButtonSprite.GetSprite());
                paintButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.CanMove;
                paintButton.Visibility = (button) => !MyPlayer.IsDead;
                paintButton.OnClick = (button) => {
                    var invoker = PlayerModInfo.RpcAddOutfit.GetInvoker(new(sampleTracker.CurrentTarget!.PlayerId, new("Paint", 40, false, sample ?? MyPlayer.Unbox().GetOutfit(75))));
                    if (MyRole.TransformAfterMeetingOption)
                        NebulaGameManager.Instance?.Scheduler.Schedule(RPCScheduler.RPCTrigger.AfterMeeting, invoker);
                    else
                        invoker.InvokeSingle();
                    button.StartCoolDown();

                    acTokenPainterCommon ??= new("painter.common1");
                    if (acTokenMorphingCommon != null) acTokenCommon ??= new("illusioner.common1");
                };
                paintButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        paintButton.ResetKeyBind();
                        morphButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                        morphButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                    });
                };
                paintButton.OnMeeting = (button) =>
                {
                    if (MyRole.LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                        sample = null;
                    }
                };
                paintButton.CoolDownTimer = Bind(new Timer(MyRole.PaintCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                paintButton.SetLabel("paint");
            }
        }
    }
}

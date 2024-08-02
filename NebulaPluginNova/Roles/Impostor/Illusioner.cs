using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Illusioner : DefinedRoleTemplate, DefinedRole
{
    private Illusioner() : base("illusioner", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SampleCoolDownOption, MorphCoolDownOption,MorphDurationOption,PaintCoolDownOption, LoseSampleOnMeetingOption, TransformAfterMeetingOption,SampleOriginalLookOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagChaotic, ConfigurationTags.TagDifficult);
        //ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Illusioner.png");
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[]? arguments) => new Instance(player);

    static private FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.sampleCoolDown", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MorphCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.morphCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MorphDurationOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.morphDuration", (5f, 120f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration PaintCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.paintCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration LoseSampleOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.loseSampleOnMeeting", false);
    static private BoolConfiguration TransformAfterMeetingOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.transformAfterMeeting", false);
    static private BoolConfiguration SampleOriginalLookOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.sampleOriginalLook", false);

    static public Illusioner MyRole = new Illusioner();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? sampleButton = null;
        private ModAbilityButton? morphButton = null;
        private ModAbilityButton? paintButton = null;

        StaticAchievementToken? acTokenMorphingCommon = null, acTokenPainterCommon = null, acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;

        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("illusioner.challenge", 0, (val, _) =>
                {
                    return
                    NebulaGameManager.Instance!.AllPlayerInfo().Where(p => p.PlayerState == PlayerState.Exiled && (val & (1 << p.PlayerId)) != 0).Count() > 0 &&
                    NebulaGameManager.Instance!.AllPlayerInfo().Where(p => (p.MyKiller?.AmOwner ?? false) && (val & (1 << p.PlayerId)) != 0).Count() > 0;
                });

                Outfit? sample = null;
                PoolablePlayer? sampleIcon = null;
                var sampleTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                sampleButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "illusioner.sample");
                sampleButton.SetSprite(Morphing.Instance.SampleButtonSprite.GetSprite());
                sampleButton.Availability = (button) => MyPlayer.CanMove;
                sampleButton.Visibility = (button) => !MyPlayer.IsDead;
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker.CurrentTarget?.GetOutfit(SampleOriginalLookOption ? 35 : 75) ?? null;
                    if (sample != null) acTokenChallenge.Value |= 1 << sample.outfit.ColorId;

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample.outfit, sampleButton.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                };
                sampleButton.CoolDownTimer = Bind(new Timer(SampleCoolDownOption).SetAsAbilityCoolDown().Start());
                sampleButton.SetLabel("sample");

                morphButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.morph").SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction,"illusioner.switch");
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
                        paintButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility,"illusioner.paint");
                        paintButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction, "illusioner.switch");
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

                    if (LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                        sample = null;
                    }
                };
                morphButton.CoolDownTimer = Bind(new Timer(MorphCoolDownOption).SetAsAbilityCoolDown().Start());
                morphButton.EffectTimer = Bind(new Timer(MorphDurationOption));
                morphButton.SetLabel("morph");

                paintButton = Bind(new ModAbilityButton());
                paintButton.SetSprite(Painter.Instance.PaintButtonSprite.GetSprite());
                paintButton.Availability = (button) => sampleTracker.CurrentTarget != null && MyPlayer.CanMove;
                paintButton.Visibility = (button) => !MyPlayer.IsDead;
                paintButton.OnClick = (button) => {
                    var invoker = PlayerModInfo.RpcAddOutfit.GetInvoker(new(sampleTracker.CurrentTarget!.PlayerId, new("Paint", 40, false, sample ?? MyPlayer.GetOutfit(75))));
                    if (TransformAfterMeetingOption)
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
                        morphButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.morph");
                        morphButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction, "illusioner.switch");
                    });
                };
                paintButton.OnMeeting = (button) =>
                {
                    if (LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                        sample = null;
                    }
                };
                paintButton.CoolDownTimer = Bind(new Timer(PaintCoolDownOption).SetAsAbilityCoolDown().Start());
                paintButton.SetLabel("paint");
            }
        }
    }
}

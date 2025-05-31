using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Illusioner : DefinedSingleAbilityRoleTemplate<Illusioner.Ability>, DefinedRole
{
    private Illusioner() : base("illusioner", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SampleCoolDownOption, MorphCoolDownOption,MorphDurationOption,PaintCoolDownOption, LoseSampleOnMeetingOption, TransformAfterMeetingOption,SampleOriginalLookOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagChaotic, ConfigurationTags.TagDifficult);
        //ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Illusioner.png");
    }


    static private readonly FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.sampleCoolDown", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration MorphCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.morphCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration MorphDurationOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.morphDuration", (5f, 120f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration PaintCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.paintCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration LoseSampleOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.loseSampleOnMeeting", false);
    static private readonly BoolConfiguration TransformAfterMeetingOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.transformAfterMeeting", false);
    static private readonly BoolConfiguration SampleOriginalLookOption = NebulaAPI.Configurations.Configuration("options.role.illusioner.sampleOriginalLook", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static public Illusioner MyRole = new Illusioner();
    static private GameStatsEntry StatsSample = NebulaAPI.CreateStatsEntry("stats.illusioner.sample", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsMorph = NebulaAPI.CreateStatsEntry("stats.illusioner.morph", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsPaint = NebulaAPI.CreateStatsEntry("stats.illusioner.paint", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        StaticAchievementToken? acTokenMorphingCommon = null, acTokenPainterCommon = null, acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                acTokenChallenge = new("illusioner.challenge", 0, (val, _) =>
                {
                    return
                    NebulaGameManager.Instance!.AllPlayerInfo.Where(p => p.PlayerState == PlayerState.Exiled && (val & (1 << p.PlayerId)) != 0).Count() > 0 &&
                    NebulaGameManager.Instance!.AllPlayerInfo.Where(p => (p.MyKiller?.AmOwner ?? false) && (val & (1 << p.PlayerId)) != 0).Count() > 0;
                });

                OutfitDefinition? sample = null;
                PoolablePlayer? sampleIcon = null;
                var sampleTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate).Register(this);

                var sampleButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "illusioner.sample",
                    SampleCoolDownOption, "sample", Morphing.Ability.SampleButtonSprite).SetAsUsurpableButton(this);
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker.CurrentTarget?.GetOutfit(SampleOriginalLookOption ? 35 : 75) ?? null;
                    if (sample != null) acTokenChallenge.Value |= 1 << sample.outfit.ColorId;

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample.outfit, (sampleButton as ModAbilityButtonImpl)!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                    StatsSample.Progress();
                };

                ModAbilityButton morphButton = null!, paintButton = null!;

                morphButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.morph",
                    MorphCoolDownOption, MorphDurationOption, "morph", Morphing.Ability.MorphButtonSprite, _ => sample != null, isToggleEffect: true)
                    .BindSubKey(Virial.Compat.VirtualKeyInput.AidAction,"illusioner.switch").SetAsUsurpableButton(this);
                morphButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        morphButton.ResetKeyBinding();
                        paintButton.BindKey(Virial.Compat.VirtualKeyInput.SecondaryAbility,"illusioner.paint");
                        paintButton.BindKey(Virial.Compat.VirtualKeyInput.AidAction, "illusioner.switch");
                    });
                };
                morphButton.OnEffectStart = (button) =>
                {
                    PlayerModInfo.RpcAddOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, new(sample!, "Morphing", 50, true)));

                    acTokenMorphingCommon ??= new("morphing.common1");
                    if (acTokenPainterCommon != null) acTokenCommon ??= new("illusioner.common1");
                    StatsMorph.Progress();
                };
                morphButton.OnEffectEnd = (button) =>
                {
                    PlayerModInfo.RpcRemoveOutfit.Invoke(new(PlayerControl.LocalPlayer.PlayerId, "Morphing"));
                    morphButton.CoolDownTimer?.Start();
                };

                paintButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.None,
                    PaintCoolDownOption, "paint", Painter.Ability.PaintButtonSprite, _ => sampleTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                paintButton.OnClick = (button) => {
                    var invoker = PlayerModInfo.RpcAddOutfit.GetInvoker(new(sampleTracker.CurrentTarget!.PlayerId, new(sample ?? MyPlayer.GetOutfit(75), "Paint", 40, false)));
                    if (TransformAfterMeetingOption)
                        NebulaGameManager.Instance?.Scheduler.Schedule(RPCScheduler.RPCTrigger.AfterMeeting, invoker);
                    else
                        invoker.InvokeSingle();
                    button.StartCoolDown();

                    acTokenPainterCommon ??= new("painter.common1");
                    if (acTokenMorphingCommon != null) acTokenCommon ??= new("illusioner.common1");
                    StatsPaint.Progress();
                };
                paintButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        paintButton.ResetKeyBinding();
                        morphButton.BindKey(Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.morph");
                        morphButton.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "illusioner.switch");
                    });
                };

                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev => {
                    morphButton.InterruptEffect();

                    if (LoseSampleOnMeetingOption)
                    {
                        if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                        sampleIcon = null;
                        sample = null;
                    }
                }, this);
            }
        }
    }
}

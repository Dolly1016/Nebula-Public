using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Painter : DefinedSingleAbilityRoleTemplate<Painter.Ability>, DefinedRole
{
    private Painter() : base("painter", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [SampleCoolDownOption, PaintCoolDownOption, LoseSampleOnMeetingOption, TransformAfterMeetingOption]) { }


    static private FloatConfiguration SampleCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.painter.sampleCoolDown", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration PaintCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.painter.paintCoolDown", (0f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration LoseSampleOnMeetingOption = NebulaAPI.Configurations.Configuration("options.role.painter.loseSampleOnMeeting", false);
    static private BoolConfiguration TransformAfterMeetingOption = NebulaAPI.Configurations.Configuration("options.role.painter.transformAfterMeeting", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static public Painter MyRole = new Painter();
    static private GameStatsEntry StatsSample = NebulaAPI.CreateStatsEntry("stats.painter.sample", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsPaint = NebulaAPI.CreateStatsEntry("stats.painter.paint", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButtonImpl? sampleButton = null;
        private ModAbilityButtonImpl? paintButton = null;

        static public Image sampleButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SampleButton.png", 115f);
        static public Image PaintButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.PaintButton.png", 115f);
        

        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<int[]> acTokenChallenge = null;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped): base(player, isUsurped)
        {
            if (AmOwner)
            {
                acTokenChallenge = new("painter.challenge", new int[24], (val, _) => val.Count(v => v >= 2) >= 3);

                ModAbilityButton paintButton = null!;
                OutfitDefinition? sample = null;
                PoolablePlayer? sampleIcon = null;
                var sampleTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate).Register(this);

                var sampleButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "illusioner.sample",
                    SampleCoolDownOption, "sample", sampleButtonSprite).SetAsUsurpableButton(this);
                sampleButton.OnClick = (button) => {
                    sample = sampleTracker.CurrentTarget?.GetOutfit(75) ?? null;

                    if (sampleIcon != null) GameObject.Destroy(sampleIcon.gameObject);
                    if (sample == null) return;
                    sampleIcon = AmongUsUtil.GetPlayerIcon(sample!.outfit, (paintButton as ModAbilityButtonImpl)!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                    StatsSample.Progress();
                };

                paintButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "illusioner.paint",
                    PaintCoolDownOption, "paint", PaintButtonSprite,
                    _ => sampleTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                paintButton.OnClick = (button) => {
                    var outfit = sample ?? MyPlayer.GetOutfit(75);

                    acTokenCommon ??= new("painter.common1");
                    if (sampleTracker.CurrentTarget!.Unbox()!.GetOutfit(75).Outfit.outfit.ColorId != outfit.outfit.ColorId)
                        acTokenChallenge.Value[sampleTracker.CurrentTarget!.PlayerId]++;

                    var invoker = PlayerModInfo.RpcAddOutfit.GetInvoker(new(sampleTracker.CurrentTarget!.PlayerId, new(outfit, "Paint", 40, false)));
                    if (TransformAfterMeetingOption)
                        NebulaGameManager.Instance?.Scheduler.Schedule(RPCScheduler.RPCTrigger.AfterMeeting, invoker);
                    else
                        invoker.InvokeSingle();
                    button.StartCoolDown();
                    StatsPaint.Progress();
                };

                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                {
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


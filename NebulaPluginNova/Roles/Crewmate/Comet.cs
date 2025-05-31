using Nebula.Patches;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;


public class Comet : DefinedSingleAbilityRoleTemplate<Comet.Ability>, DefinedRole
{
    public Comet() : base("comet", new(121,175,206), RoleCategory.CrewmateRole, Crewmate.MyTeam, [BlazeCoolDownOption, BlazeDurationOption, BlazeSpeedOption, BlazeVisionOption, BlazeScreenOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Comet.png");
    }
    
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    static private readonly FloatConfiguration BlazeCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.comet.blazeCoolDown", (5f,60f,2.5f),20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration BlazeSpeedOption = NebulaAPI.Configurations.Configuration("options.role.comet.blazeSpeed", (0.5f, 3f, 0.125f), 1.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration BlazeDurationOption = NebulaAPI.Configurations.Configuration("options.role.comet.blazeDuration", (5f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration BlazeVisionOption = NebulaAPI.Configurations.Configuration("options.role.comet.blazeVisionRate", (1f, 3f, 0.125f), 1.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration BlazeScreenOption = NebulaAPI.Configurations.Configuration("options.role.comet.blazeScreenRate", (1f, 2f, 0.125f), 1.125f, FloatConfigurationDecorator.Ratio);

    static public readonly Comet MyRole = new();
    static private readonly GameStatsEntry StatsBlazing = NebulaAPI.CreateStatsEntry("stats.comet.blazing", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BoostButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                AchievementToken<bool> acTokenCommon = new((AmongUsUtil.CurrentMapId is 0 or 4) ? "comet.common1" : "comet.common2", false, (val, _) => val);
                AchievementToken<(Vector2 pos, bool cleared)>? acTokenCommon2 = null;

                if(BlazeDurationOption <= 15f && BlazeSpeedOption <= 2.5f)
                    acTokenCommon2 = new("comet.common3", (Vector2.zero, false), (val, _) => val.Item2);

                var boostButton = NebulaAPI.Modules.AbilityButton(this)
                    .BindKey(Virial.Compat.VirtualKeyInput.Ability)
                    .SetImage(buttonSprite)
                    .SetLabel("blaze")
                    .SetAsUsurpableButton(this);

                boostButton.Availability = (button) => MyPlayer.CanMove;
                boostButton.Visibility = (button) => !MyPlayer.IsDead;
                boostButton.OnClick = (button) => button.StartEffect();
                boostButton.OnEffectStart = (button) => {
                    using (RPCRouter.CreateSection("CometBlaze"))
                    {
                        MyPlayer.GainSpeedAttribute(BlazeSpeedOption, BlazeDurationOption, false, 100, "nebula::comet");
                        MyPlayer.GainAttribute(PlayerAttributes.Invisible, BlazeDurationOption, false, 100, "nebula::comet");
                        if (BlazeVisionOption > 1f) MyPlayer.GainAttribute(PlayerAttributes.Eyesight, BlazeDurationOption, BlazeVisionOption, false, 100, "nebula::comet");
                        if(BlazeScreenOption > 1f) MyPlayer.GainAttribute(PlayerAttributes.ScreenSize, BlazeDurationOption, BlazeScreenOption, false, 100, "nebula::comet");
                    }
                    acTokenCommon.Value = true;
                    if(acTokenCommon2 != null) acTokenCommon2.Value.pos = MyPlayer.VanillaPlayer.GetTruePosition();

                    if (MyPlayer.Unbox().HasAttributeByTag("nebula.trap0") && MyPlayer.Unbox().HasAttributeByTag("perkAccel")) new StaticAchievementToken("comet.common4");
                    StatsBlazing.Progress();
                };
                boostButton.OnEffectEnd = (button) =>
                {
                    boostButton.StartCoolDown();

                    //緊急会議招集による移動は除外
                    if(!MeetingHud.Instance && acTokenCommon2 != null)
                        acTokenCommon2.Value.cleared |= MyPlayer.VanillaPlayer.GetTruePosition().Distance(acTokenCommon2.Value.pos) > 45f;
                };
                boostButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, BlazeCoolDownOption).SetAsAbilityTimer().Start();
                boostButton.EffectTimer = NebulaAPI.Modules.Timer(this, BlazeDurationOption);
            }
        }

        bool IPlayerAbility.IgnoreBlackout => true;

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (MyPlayer.HasAttribute(PlayerAttributes.Invisible))
            {
                if (!Helpers.AnyNonTriggersBetween(MyPlayer.VanillaPlayer.GetTruePosition(), ev.Dead.VanillaPlayer.GetTruePosition(), out var vec) &&
                    vec.magnitude < BlazeVisionOption * 0.75f)
                    new StaticAchievementToken("comet.challenge");
            }
        }
    }
}


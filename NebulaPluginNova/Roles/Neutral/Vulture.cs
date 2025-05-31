using Nebula.Game.Statistics;
using Nebula.Roles.Abilities;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Vulture : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.vulture", new(140, 70, 18), TeamRevealType.OnlyMe);

    private Vulture() : base("vulture", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [EatCoolDownOption, NumOfEatenToWinOption, VentConfiguration]) {
        GameActionTypes.EatCorpseAction = new("vulture.eat", this, isCleanDeadBodyAction: true);
    }
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private IRelativeCoolDownConfiguration EatCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.vulture.eatCoolDown", CoolDownType.Immediate, (5f, 60f, 5f), 20f, (-40f, 20f, 5f), -10f, (0.125f, 2f, 0.125f), 0.75f);
    static private IntegerConfiguration NumOfEatenToWinOption = NebulaAPI.Configurations.Configuration("options.role.vulture.numOfTheEatenToWin", (1,8),3);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("options.role.vulture.vent", true);

    static public Vulture MyRole = new Vulture();
    static private GameStatsEntry StatsEaten = NebulaAPI.CreateStatsEntry("stats.vulture.eaten", GameStatsCategory.Roles, MyRole);
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private Modules.ScriptComponents.ModAbilityButtonImpl? eatButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EatButton.png", 115f);

        int leftEaten = NumOfEatenToWinOption;

        AchievementToken<bool>? acTokenChallenge;

        public Instance(GamePlayer player, int[] arguments) : base(player, VentConfiguration)
        {
            if (arguments.Length >= 1) leftEaten = arguments[0];
        }
        int[]? RuntimeAssignable.RoleArguments => new int[] { leftEaten };

        void OnReported(ReportDeadBodyEvent ev)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value = false;
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("vulture.challenge", true, (val, _) =>  val && NebulaGameManager.Instance!.EndState!.EndCondition == NebulaGameEnd.VultureWin && NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer) );

                //死体を指す矢印を表示する
                var ability = new DeadbodyArrowAbility().Register(this);
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => ability.ShowArrow = !MyPlayer.IsDead, this);

                StaticAchievementToken? acTokenCommon = null;

                var eatTracker = ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => true).Register(this);

                var eatButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    EatCoolDownOption.CoolDown, "eat", buttonSprite,
                    _ => eatTracker.CurrentTarget != null);
                eatButton.ShowUsesIcon(2, leftEaten.ToString());
                eatButton.OnClick = (button) => {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.EatCorpseAction);

                    AmongUsUtil.RpcCleanDeadBody(eatTracker.CurrentTarget!.PlayerId, MyPlayer.PlayerId,EventDetail.Eat);
                    leftEaten--;
                    button.UpdateUsesIcon(leftEaten.ToString());
                    eatButton.StartCoolDown();

                    acTokenCommon ??= new("vulture.common1");
                    StatsEaten.Progress();

                    if (leftEaten <= 0) NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.VultureWin, 1 << MyPlayer.PlayerId);
                };
            }
        }

        bool RuntimeRole.HasImpostorVision => true;
    }
}

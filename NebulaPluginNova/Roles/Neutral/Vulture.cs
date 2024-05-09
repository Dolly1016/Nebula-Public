using Virial;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Neutral;

public class Vulture : ConfigurableStandardRole, HasCitation
{
    static public Vulture MyRole = new Vulture();
    static public Team MyTeam = new("teams.vulture", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;

    public override string LocalizedName => "vulture";
    public override Color RoleColor => new Color(140f / 255f, 70f / 255f, 18f / 255f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    private KillCoolDownConfiguration EatCoolDownOption = null!;
    private NebulaConfiguration NumOfEatenToWinOption = null!;
    private new VentConfiguration VentConfiguration = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);

        EatCoolDownOption = new(RoleConfig, "eatCoolDown", KillCoolDownConfiguration.KillCoolDownType.Immediate, 5f, 5f, 60f, -40f, 20f, 0.125f, 0.125f, 2f, 20f, -10f, 0.5f);
        NumOfEatenToWinOption = new NebulaConfiguration(RoleConfig, "numOfTheEatenToWin", null, 1, 8, 3, 3);
    }


    public class Instance : RoleInstance, IGamePlayerOperator
    {
        private ModAbilityButton? eatButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EatButton.png", 115f);

        public override AbstractRole Role => MyRole;

        private Timer ventCoolDown = new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;
        int leftEaten = MyRole.NumOfEatenToWinOption;

        AchievementToken<bool>? acTokenChallenge;

        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length >= 1) leftEaten = arguments[0];
        }
        public override int[]? GetRoleArgument() => new int[] { leftEaten };

        private List<(DeadBody deadBody, Arrow arrow)> AllArrows = new();
        void IGameOperator.OnDeadBodyGenerated(DeadBody deadBody)
        {
            if(AmOwner) AllArrows.Add((deadBody, Bind(new Arrow(null) { TargetPos = deadBody.TruePosition }.SetColor(Color.blue))));
        }

        public override void LocalUpdate()
        {
            AllArrows.RemoveAll((tuple) =>
            {
                if (tuple.deadBody)
                {
                    tuple.arrow.TargetPos = tuple.deadBody.TruePosition;
                    return false;
                }
                else
                {
                    tuple.arrow.ReleaseIt();
                    return true;
                }
            });
        }

        void IGameOperator.OnReported(GamePlayer reporter, GamePlayer reported)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value = false;
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                acTokenChallenge = new("vulture.challenge", true, (val, _) =>  val && NebulaEndState.CurrentEndState!.EndCondition == NebulaGameEnd.VultureWin && NebulaEndState.CurrentEndState!.CheckWin(MyPlayer.PlayerId) );

                StaticAchievementToken? acTokenCommon = null;

                var eatTracker = Bind(ObjectTrackers.ForDeadBody(null, MyPlayer, (d) => true));

                eatButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                var usesIcon = eatButton.ShowUsesIcon(2);
                eatButton.SetSprite(buttonSprite.GetSprite());
                eatButton.Availability = (button) => eatTracker.CurrentTarget != null && MyPlayer.CanMove;
                eatButton.Visibility = (button) => !MyPlayer.IsDead;
                eatButton.OnClick = (button) => {
                    AmongUsUtil.RpcCleanDeadBody(eatTracker.CurrentTarget!.PlayerId, MyPlayer.PlayerId,EventDetail.Eat);
                    leftEaten--;
                    usesIcon.text=leftEaten.ToString();
                    eatButton.StartCoolDown();

                    acTokenCommon ??= new("vulture.common1");

                    if (leftEaten <= 0) NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.VultureWin, 1 << MyPlayer.PlayerId);
                };
                eatButton.CoolDownTimer = Bind(new Timer(MyRole.EatCoolDownOption.CurrentCoolDown).SetAsAbilityCoolDown().Start());
                eatButton.SetLabel("eat");
                usesIcon.text= leftEaten.ToString();
            }
        }

        public override bool HasImpostorVision => true;
    }
}

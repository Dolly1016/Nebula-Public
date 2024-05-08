using Virial.Assignable;

namespace Nebula.Roles.Crewmate;

public class Sheriff : ConfigurableStandardRole, HasCitation
{
    static public Sheriff MyRole = new Sheriff();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "sheriff";
    public override Color RoleColor => new Color(240f / 255f, 191f / 255f, 0f);
    Citation? HasCitation.Citaion => Citations.TheOtherRoles;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player,arguments);

    private KillCoolDownConfiguration KillCoolDownOption = null!;
    private NebulaConfiguration NumOfShotsOption = null!;
    private NebulaConfiguration CanKillMadmateOption = null!;
    private NebulaConfiguration CanKillHidingPlayerOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        KillCoolDownOption = new(RoleConfig, "killCoolDown", KillCoolDownConfiguration.KillCoolDownType.Relative, 2.5f, 10f, 60f, -40f, 40f, 0.125f, 0.125f, 2f, 25f, -5f, 1f);
        NumOfShotsOption = new(RoleConfig, "numOfShots", null, 1, 15, 3, 3);
        CanKillMadmateOption = new(RoleConfig, "canKillMadmate", null, false, false);
        CanKillHidingPlayerOption = new(RoleConfig, "canKillHidingPlayer", null, false, false);
    }

    public class Instance : Crewmate.Instance
    {
        private ModAbilityButton? killButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SheriffKillButton.png", 100f);
        public override AbstractRole Role => MyRole;
        private int leftShots = MyRole.NumOfShotsOption;
        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if(arguments.Length >= 1) leftShots = arguments[0];
        }
        public override int[]? GetRoleArgument() => new int[] { leftShots };

        private AchievementToken<bool>? acTokenShot;
        private AchievementToken<bool>? acTokenMisshot;
        private AchievementToken<int>? acTokenChallenge;

        public override void OnGameStart()
        {
            base.OnGameStart();
            if (AmOwner) {
                int impostors = NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.Role.Role.Category == RoleCategory.ImpostorRole) ?? 0;
                if(impostors > 0) acTokenChallenge = new("sheriff.challenge", impostors, (val, _) => val == 0);
            }
        }

        private bool CanKill(GamePlayer target)
        {
            if (target.Role.Role == Madmate.MyRole) return Sheriff.MyRole.CanKillMadmateOption;
            if (target.Role.Role.Category == RoleCategory.CrewmateRole) return false;
            return true;
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                acTokenShot = new("sheriff.common1",false,(val,_)=>val);
                acTokenMisshot = new("sheriff.another1", false, (val, _) => val);

                var killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => !p.AmOwner && !p.IsDead, null, MyRole.CanKillHidingPlayerOption));
                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);

                var leftText = killButton.ShowUsesIcon(3);
                leftText.text = leftShots.ToString();

                killButton.SetSprite(buttonSprite.GetSprite());
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.Visibility = (button) => !MyPlayer.IsDead && leftShots > 0;
                killButton.OnClick = (button) => {
                    if (CanKill(killTracker.CurrentTarget!))
                    {
                        acTokenShot!.Value = true;
                        if (acTokenChallenge != null && killTracker.CurrentTarget!.IsImpostor) acTokenChallenge!.Value--;

                        MyPlayer.MurderPlayer(killTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill);
                    }
                    else
                    {
                        MyPlayer.Suicide(PlayerState.Misfired, null);
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Misfire, MyPlayer.VanillaPlayer, killTracker.CurrentTarget!.VanillaPlayer);

                        acTokenMisshot!.Value = true;
                    }
                    button.StartCoolDown();

                    leftText.text = (--leftShots).ToString();
                };
                killButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabel("kill");
            }
        }

    }
}


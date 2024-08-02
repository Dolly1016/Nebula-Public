using Nebula.Game.Statistics;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Sheriff : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Sheriff():base("sheriff", new(240,191,0), RoleCategory.CrewmateRole, Crewmate.MyTeam, [KillCoolDownOption, NumOfShotsOption, CanKillMadmateOption, CanKillLoversOption, CanKillHidingPlayerOption, SealAbilityUntilReportingDeadBodiesOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    Citation? HasCitation.Citaion => Citations.TheOtherRoles;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player,arguments);

    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sheriff.killCoolDown", CoolDownType.Relative, (10f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static private IntegerConfiguration NumOfShotsOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.numOfShots", (1, 15), 3);
    static private BoolConfiguration CanKillMadmateOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.canKillMadmate", false);
    static private BoolConfiguration CanKillLoversOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.canKillLovers", false);
    static private BoolConfiguration CanKillHidingPlayerOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.canKillHidingPlayer", false);
    static private BoolConfiguration SealAbilityUntilReportingDeadBodiesOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.sealAbilityUntilReportingDeadBodies", false);

    static public Sheriff MyRole = new Sheriff();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? killButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SheriffKillButton.png", 100f);
        
        private int leftShots = NumOfShotsOption;
        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if(arguments.Length >= 1) leftShots = arguments[0];
        }
        int[]? RuntimeAssignable.RoleArguments => [leftShots];

        private AchievementToken<(byte,bool)>? acTokenCommon2;
        private AchievementToken<bool>? acTokenAnother2;
        private AchievementToken<int>? acTokenChallenge;

        [Local]
        void OnGameStart(GameStartEvent ev)
        {
            int impostors = NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.Role.Role.Category == RoleCategory.ImpostorRole) ?? 0;
            if (impostors > 0) acTokenChallenge = new("sheriff.challenge", impostors, (val, _) => val == 0);
        }

        private bool CanKill(GamePlayer target)
        {
            if (target.Role.Role == Madmate.MyRole) return CanKillMadmateOption;
            if (target.TryGetModifier<Lover.Instance>(out _) && CanKillLoversOption) return true;
            if (target.Role.Role.Category == RoleCategory.CrewmateRole) return false;
            return true;
        }

        void OnMeetingEnd(MeetingVoteDisclosedEvent ev)
        {
            if (acTokenCommon2 != null)
                acTokenCommon2.Value.Item1 = ev.VoteStates.FirstOrDefault(v => v.VoterId == MyPlayer.PlayerId).VotedForId;
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenCommon2 = new("sheriff.common2", (byte.MaxValue,false), (val,_) => val.Item2);
                acTokenAnother2 = new("sheriff.another2", true, (val, _) => val && NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnds.CrewmateGameEnd && !MyPlayer.IsDead);

                var killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.StandardPredicate(p), null, CanKillHidingPlayerOption));
                killButton = Bind(new ModAbilityButton(isArrangedAsKillButton: true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);

                var leftText = killButton.ShowUsesIcon(3);
                leftText.text = leftShots.ToString();

                killButton.SetSprite(buttonSprite.GetSprite());

                SpriteRenderer? lockSprite = null;
                //封印設定が有効かつ死者がいない場合に能力を一時的に封印する。
                if (SealAbilityUntilReportingDeadBodiesOption && !NebulaGameManager.Instance!.AllPlayerInfo().Any(p => p.IsDead))
                {
                    lockSprite = killButton.VanillaButton.AddLockedOverlay();
                }

                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.CanMove && lockSprite == null;
                killButton.Visibility = (button) => !MyPlayer.IsDead && leftShots > 0;
                killButton.OnClick = (button) => {
                    acTokenAnother2.Value = false;
                    if (CanKill(killTracker.CurrentTarget!))
                    {
                        new StaticAchievementToken("sheriff.common1");
                        acTokenCommon2.Value.Item2 |= acTokenCommon2.Value.Item1 == killTracker.CurrentTarget!.PlayerId;
                        if (acTokenChallenge != null && killTracker.CurrentTarget!.IsImpostor) acTokenChallenge!.Value--;

                        MyPlayer.MurderPlayer(killTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);
                    }
                    else
                    {
                        MyPlayer.Suicide(PlayerState.Misfired, null, Virial.Game.KillParameter.NormalKill);
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Misfire, MyPlayer.VanillaPlayer, killTracker.CurrentTarget!.VanillaPlayer);

                        new StaticAchievementToken("sheriff.another1");
                    }
                    button.StartCoolDown();

                    leftText.text = (--leftShots).ToString();
                };
                killButton.CoolDownTimer = Bind(new Timer(KillCoolDownOption.CoolDown).SetAsKillCoolDown().Start());
                killButton.SetLabel("kill");
                killButton.OnMeeting = button =>
                {
                    if(lockSprite && NebulaGameManager.Instance!.AllPlayerInfo().Any(p => p.IsDead))
                    {
                        GameObject.Destroy(lockSprite!.gameObject);
                        lockSprite = null;
                    }
                };
            }
        }

    }
}


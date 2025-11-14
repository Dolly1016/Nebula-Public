using Nebula.Game.Statistics;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Events.Role;
using Virial.Game;
using Virial.Helpers;
using Virial.Text;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
public class Sheriff : DefinedSingleAbilityRoleTemplate<Sheriff.Ability>, HasCitation, DefinedRole
{
    private record MisfiredExtraDeadInfo(GamePlayer Target) : GamePlayer.ExtraDeadInfo(PlayerStates.Misfired)
    {
        public override string ToStateText() => "for " + Target.PlayerName;
    }
    static private readonly RemoteProcess<(GamePlayer sheriff, GamePlayer target)> RpcShareExtraInfo = new("ShareExInfoSheriff",
        (message, _) => {
            message.sheriff.PlayerStateExtraInfo = new MisfiredExtraDeadInfo(message.target);
        }
    );

    private Sheriff():base("sheriff", new(240,191,0), RoleCategory.CrewmateRole, Crewmate.MyTeam, [KillCoolDownOption, NumOfShotsOption, CanKillMadmateOption, CanKillLoversOption, CanKillHidingPlayerOption, SealAbilityUntilReportingDeadBodiesOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Sheriff.png");
        ConfigurationHolder?.ScheduleAddRelated(() => [Neutral.Vanity.MyRole.ConfigurationHolder!]);
    }

    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1,NumOfShotsOption));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static internal readonly IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.sheriff.killCoolDown", CoolDownType.Relative, (10f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static internal readonly IntegerConfiguration NumOfShotsOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.numOfShots", (1, 15), 3);
    static private readonly BoolConfiguration CanKillMadmateOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.canKillMadmate", false);
    static private readonly BoolConfiguration CanKillLoversOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.canKillLovers", false);
    static internal readonly BoolConfiguration CanKillHidingPlayerOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.canKillHidingPlayer", false);
    static internal readonly BoolConfiguration SealAbilityUntilReportingDeadBodiesOption = NebulaAPI.Configurations.Configuration("options.role.sheriff.sealAbilityUntilReportingDeadBodies", false);

    static public readonly Sheriff MyRole = new();
    static private readonly GameStatsEntry StatsShot = NebulaAPI.CreateStatsEntry("stats.sheriff.shot", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsMisshot = NebulaAPI.CreateStatsEntry("stats.sheriff.misshot", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButtonImpl? killButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SheriffKillButton.png", 100f);
        static internal Image KillButtonSprite => buttonSprite;

        private int leftShots = NumOfShotsOption;
        public Ability(GamePlayer player, bool isUsurped, int shots) : base(player, isUsurped)
        {
            leftShots = shots;

            if (AmOwner)
            {
                var acTokenAnother3 = AbstractAchievement.GenerateSimpleTriggerToken("sheriff.another3");
                GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev => { acTokenAnother3.Value.triggered = false; }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerExiledEvent>(ev => { if (ev.Player.AmOwner) acTokenAnother3.Value.isCleared |= acTokenAnother3.Value.triggered; }, this);

                acTokenCommon2 = new("sheriff.common2", (byte.MaxValue, false), (val, _) => val.Item2);
                acTokenAnother2 = new("sheriff.another2", true, (val, _) => val && NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnd.CrewmateWin && !MyPlayer.IsDead);

                var killTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, ObjectTrackers.PlayerlikeKillablePredicate(MyPlayer), null, CanKillHidingPlayerOption);
                killButton = new ModAbilityButtonImpl(isArrangedAsKillButton: MyPlayer.IsCrewmate).KeyBind(MyPlayer.IsCrewmate ? Virial.Compat.VirtualKeyInput.Kill : Virial.Compat.VirtualKeyInput.Ability).Register(this);

                var leftText = killButton.ShowUsesIcon(3);
                leftText.text = leftShots.ToString();

                killButton.SetSprite(buttonSprite.GetSprite());

                SpriteRenderer? lockSprite = null;
                //封印設定が有効かつ死者がいない場合に能力を一時的に封印する。
                if (SealAbilityUntilReportingDeadBodiesOption && !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => p.IsDead))
                {
                    lockSprite = killButton.VanillaButton.AddLockedOverlay();
                }

                bool killedAnyone = false;
                killButton.Availability = (button) => killTracker.CurrentTarget != null && MyPlayer.CanMove && !MyPlayer.WillDie && lockSprite == null;
                killButton.Visibility = (button) => !MyPlayer.IsDead && leftShots > 0;
                killButton.OnClick = (button) => {
                    acTokenAnother2.Value = false;
                    StatsShot.Progress();
                    if (!MyPlayer.IsMadmate && CanKill(killTracker.CurrentTarget!.RealPlayer!))
                    {
                        new StaticAchievementToken("sheriff.common1");
                        acTokenCommon2.Value.Item2 |= acTokenCommon2.Value.Item1 == killTracker.CurrentTarget!.RealPlayer.PlayerId;
                        if (acTokenChallenge != null && killTracker.CurrentTarget!.RealPlayer.IsImpostor) acTokenChallenge!.Value--;

                        MyPlayer.MurderPlayer(killTracker.CurrentTarget!, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);

                        acTokenAnother3.Value.triggered = true;
                        killedAnyone = true;
                    }
                    else
                    {
                        MyPlayer.Suicide(PlayerState.Misfired, PlayerState.Suicide, Virial.Game.KillParameter.NormalKill);
                        NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.Misfire, MyPlayer.VanillaPlayer, killTracker.CurrentTarget!.RealPlayer.VanillaPlayer);
                        RpcShareExtraInfo.Invoke((MyPlayer, killTracker.CurrentTarget!.RealPlayer));

                        new StaticAchievementToken("sheriff.another1");
                        StatsMisshot.Progress();
                    }
                    button.StartCoolDown();

                    leftText.text = (--leftShots).ToString();
                };
                killButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown)).SetAsKillCoolTimer();
                killButton.CoolDownTimer.Start();
                killButton.SetLabel("kill");
                killButton.OnMeeting = button =>
                {
                    if (lockSprite && NebulaGameManager.Instance!.AllPlayerInfo.Any(p => p.IsDead))
                    {
                        GameObject.Destroy(lockSprite!.gameObject);
                        lockSprite = null;
                    }
                };
                killButton.RelatedAbility = this;

                if((Neutral.Vanity.MyRole as DefinedRole).IsSpawnable)
                {
                    GamePlayer? lastMyExile = null;
                    GameOperatorManager.Instance?.Subscribe<PlayerVoteDisclosedLocalEvent>(ev =>
                    {
                        if (ev.VoteToWillBeExiled) lastMyExile = ev.VoteFor;
                    }, this);
                    GameOperatorManager.Instance?.SubscribeAchievement<GameEndEvent>("combination.2.sheriff.vanity.common2", ev => lastMyExile != null && NebulaGameManager.Instance?.LastDead == lastMyExile && !ev.EndState.Winners.Test(MyPlayer), this);
                }
            }
        }
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), leftShots];

        private AchievementToken<(byte,bool)>? acTokenCommon2;
        private AchievementToken<bool>? acTokenAnother2;
        private AchievementToken<int>? acTokenChallenge;

        [Local]
        void OnGameStart(GameStartEvent ev)
        {
            int impostors = NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.Role.Role.Category == RoleCategory.ImpostorRole) ?? 0;
            if (impostors >= 2) acTokenChallenge = new("sheriff.challenge", impostors, (val, _) => val == 0);
        }

        private bool CanKill(GamePlayer target)
        {
            bool canKill = true;
            if (target.Role.Role == Madmate.MyRole) canKill = CanKillMadmateOption;
            else if (target.Role is JekyllAndHyde.Instance jah && !jah.AmJekyll) canKill = CanKillMadmateOption;
            else if (target.TryGetModifier<Lover.Instance>(out _) && CanKillLoversOption) canKill = true;
            else if (target.Role.Role.Category == RoleCategory.CrewmateRole) canKill = false;
            else canKill = true;

            return GameOperatorManager.Instance?.Run(new SheriffCheckKillEvent(MyPlayer, target, canKill)).CanKill ?? canKill;
        }

        void OnMeetingEnd(MeetingVoteDisclosedEvent ev)
        {
            if (acTokenCommon2 != null)
                acTokenCommon2.Value.Item1 = ev.VoteStates.FirstOrDefault(v => v.VoterId == MyPlayer.PlayerId).VotedForId;
        }
    }
}


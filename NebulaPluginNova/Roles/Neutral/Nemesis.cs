using Nebula.Game.Statistics;
using Nebula.Roles.Complex;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Roles.Neutral;

public class Nemesis : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.nemesis", new(120, 60, 180), TeamRevealType.OnlyMe);

    static internal IntegerConfiguration NumOfGuessToWinOption = NebulaAPI.Configurations.Configuration("options.role.nemesis.numOfGuessToWin", (1, 10), 2);
    static private IntegerConfiguration NumOfGuessPerMeetingOption = NebulaAPI.Configurations.Configuration("options.role.nemesis.numOfGuessPerMeeting", (1, 10), 1);
    static private IntegerConfiguration NumOfMissAllowedOption = NebulaAPI.Configurations.Configuration("options.role.nemesis.numOfMissAllowed", (0, 20), 1);
    static private BoolConfiguration DisableGuessAfterMissOption = NebulaAPI.Configurations.Configuration("options.role.nemesis.disableGuessAfterMiss", false);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("options.role.nemesis.vent", true);

    private Nemesis() : base("nemesis", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [NumOfGuessToWinOption, NumOfGuessPerMeetingOption, NumOfMissAllowedOption, DisableGuessAfterMissOption, Guesser.GuessableFilterEditorOption, VentConfiguration])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagChaotic);
        ConfigurationHolder?.ScheduleAddRelated(() => [Guesser.MyNiceRole.ConfigurationHolder!, Guesser.MyEvilRole.ConfigurationHolder!, GuesserModifier.MyRole.ConfigurationHolder!]);
    }

    static public Nemesis MyRole = new Nemesis();
    static private GameStatsEntry StatsGuess = NebulaAPI.CreateStatsEntry("stats.nemesis.guess", GameStatsCategory.Roles, MyRole, annotation: GameStatsAnnotation.From2611);
    static private GameStatsEntry StatsMisguess = NebulaAPI.CreateStatsEntry("stats.nemesis.misguess", GameStatsCategory.Roles, MyRole, annotation: GameStatsAnnotation.From2611);
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    Image? DefinedAssignable.IconImage => Guesser.IconImage;
    //既定では、モディファイアのゲッサーにはならない
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable != GuesserModifier.MyRole;

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => false;
    bool IAssignableDocument.HasWinCondition => true;
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%NUM%", NumOfGuessToWinOption.GetValue().ToString());
    }

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private int numOfGuessed = 0;
        private int leftMiss = NumOfMissAllowedOption;
        private bool missedThisMeeting = false;

        public Instance(GamePlayer player, int[] arguments) : base(player, VentConfiguration)
        {
            numOfGuessed = arguments.Get(0, 0);
            leftMiss = arguments.Get(1, NumOfMissAllowedOption);
        }

        int[]? RuntimeAssignable.RoleArguments => [numOfGuessed, leftMiss];

        private int LeftGuessToWin => Math.Max(0, NumOfGuessToWinOption - numOfGuessed);

        private void UpdateWinningOpportunity() => ModSingleton<IWinningOpportunity>.Instance?.RpcSetOpportunity(MyTeam, Math.Max(0f, 1f - 0.2f * LeftGuessToWin));
        private void UpdateTaskProgress() => MyPlayer.Tasks.Custom(Math.Min(numOfGuessed, NumOfGuessToWinOption), NumOfGuessToWinOption, false);

        RoleTaskType RuntimeRole.TaskType => RoleTaskType.SpecialRoleTask;

        AchievementToken<bool>? acTokenChallenge;

        /// <summary>
        /// ゲッサー(Nice/Evil Guesserとモディファイアのゲッサー)かどうかです。
        /// </summary>
        static private bool IsGuesser(GamePlayer player) => player.Role.Role is Guesser || player.Modifiers.Any(m => m.Modifier == GuesserModifier.MyRole);

        public override void OnActivated()
        {
            if (AmOwner)
            {
                //ゲッサーのいるゲームで、自身のみが推察に成功して勝利する
                acTokenChallenge = new("nemesis.challenge", true, (val, _) =>
                {
                    if (!val) return false;
                    var game = NebulaGameManager.Instance;
                    if (game?.EndState == null || !game.EndState.Winners.Test(MyPlayer)) return false;
                    var players = game.AllPlayerInfo;
                    if (!players.Any(p => p != MyPlayer && IsGuesser(p))) return false;
                    var guessed = players.Where(p => p.PlayerState == PlayerState.Guessed).ToArray();
                    return guessed.Length > 0 && guessed.All(p => p.MyKiller == MyPlayer);
                });
                UpdateWinningOpportunity();
                UpdateTaskProgress();
            }
        }

        [Local]
        void OnGameStart(GameStartEvent ev) => UpdateTaskProgress();

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            missedThisMeeting = false;
            GuesserSystem.OnMeetingStart(new GuessMeetingSetting(LeftGuessToWin, NumOfGuessPerMeetingOption)
            {
                StatsGuess = StatsGuess,
                StatsMisguess = StatsMisguess,
                ConsumeOnMisguess = false,
                OnMisguess = () =>
                {
                    if (leftMiss <= 0) return true;
                    leftMiss--;
                    if (DisableGuessAfterMissOption) missedThisMeeting = true;
                    return false;
                },
                CanGuess = () => !missedThisMeeting,
                UnderText = (leftGuessPerMeeting, leftGuess) =>
                    Language.Translate("role.guesser.leftGuess") + " : " + Math.Min(leftGuessPerMeeting, leftGuess) + "  " +
                    Language.Translate("role.nemesis.leftMiss") + " : " + leftMiss,
            });
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            GuesserSystem.OnDead();
            //推察に失敗して死亡する
            if (MyPlayer.PlayerState == PlayerState.Misguessed) new StaticAchievementToken("nemesis.another1");
        }

        //推察の成功回数は全員の視点で数え、勝利はホストが即座にトリガーする。
        [OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (ev.Dead == MyPlayer || ev.Dead.PlayerState != PlayerState.Guessed) return;

            numOfGuessed++;
            if (AmOwner)
            {
                new StaticAchievementToken("nemesis.common1");
                UpdateWinningOpportunity();
                UpdateTaskProgress();
            }
            if ((GamePlayer.LocalPlayer?.AmHost ?? false) && !MyPlayer.IsDead && LeftGuessToWin <= 0) NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.NemesisWin, 1 << MyPlayer.PlayerId, NemesisDetails.Win);
        }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            bool isCompleted = LeftGuessToWin <= 0;
            var text = Language.Translate("role.nemesis.taskText");
            if (!isCompleted) text += $" ({numOfGuessed}/{(int)NumOfGuessToWinOption})";
            ev.AppendText(text.Color(isCompleted ? VColor.Green : numOfGuessed > 0 ? VColor.Yellow : VColor.White));
        }
    }
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
file static class NemesisDetails
{
    static internal CommunicableTextTag Win = null!;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        Win = preprocessor.RegisterCommunicableText("end.detail.nemesis.win");
    }
}

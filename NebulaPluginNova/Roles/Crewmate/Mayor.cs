using System.Text;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Mayor : DefinedSingleAbilityRoleTemplate<Mayor.Ability>, HasCitation, DefinedRole
{
    private Mayor() : base("mayor", new(30,96,85), RoleCategory.CrewmateRole, Crewmate.MyTeam, [FixedVotesOption, MinVoteOption, MaxVoteOption, MaxVoteStockOption, VoteAssignmentOption, VoteAssignmentOnReportingOption, VoteAssignmentPerTasksOption]) { }
    Citation? HasCitation.Citation => Citations.TownOfImpostors;
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, 0));

    static private readonly BoolConfiguration FixedVotesOption = NebulaAPI.Configurations.Configuration("options.role.mayor.fixedVotes", false);
    static private readonly IntegerConfiguration MinVoteOption = NebulaAPI.Configurations.Configuration("options.role.mayor.minVote", (0, 20), 1, () => !FixedVotesOption);
    static private readonly IntegerConfiguration MaxVoteOption = NebulaAPI.Configurations.Configuration("options.role.mayor.maxVote", (1, 20), 2, () => !FixedVotesOption);
    static private readonly IntegerConfiguration MaxVoteStockOption = NebulaAPI.Configurations.Configuration("options.role.mayor.maxVotesStock", (int[])[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 30, 40, 50], 8, () => !FixedVotesOption);
    static private readonly IntegerConfiguration VoteAssignmentOption = NebulaAPI.Configurations.Configuration("options.role.mayor.voteAssignment", (-3, 20), 1);
    static private readonly IntegerConfiguration VoteAssignmentOnReportingOption = NebulaAPI.Configurations.Configuration("options.role.mayor.voteAssignmentOnReporting", (-3, 20), 0, () => !FixedVotesOption);
    static private readonly IntegerConfiguration VoteAssignmentPerTasksOption = NebulaAPI.Configurations.Configuration("options.role.mayor.voteAssignmentPerTasks", (-3, 20), 0, () => !FixedVotesOption);

    static public readonly Mayor MyRole = new();
    static private readonly GameStatsEntry StatsVotes = NebulaAPI.CreateStatsEntry("stats.mayor.votes", GameStatsCategory.Roles, MyRole);
    static private int MinVote => FixedVotesOption ? VoteAssignmentOption : MinVoteOption;
    static private int MaxVote => FixedVotesOption ? VoteAssignmentOption : MaxVoteOption;
    static private int VoteAssignment => VoteAssignmentOption;
    static private int VotesStock => FixedVotesOption ? VoteAssignmentOption : MaxVoteStockOption;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[]? IPlayerAbility.AbilityArguments => [myVote];

        static private readonly SpriteLoader leftButtonSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingButtonLeft.png", 100f);
        static private readonly SpriteLoader rightButtonSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingButtonRight.png", 100f);

        private int myVote = 0;
        private int currentVote = 0;

        AchievementToken<(bool cleared, byte myVotedFor, bool triggered)>? acTokenCommon = null;
        AchievementToken<(int meetings, bool clearable)>? acTokenAnother = null;
        AchievementToken<(bool cleared, int leftMeeting, bool triggered)>? acTokenCommon2 = null;
        AchievementToken<(bool cleared, byte myVotedFor, bool triggered)>? acTokenChallenge = null;


        public Ability(GamePlayer player,bool isUsurped, int vote) : base(player, isUsurped)
        {
            myVote = vote;

            if (AmOwner)
            {
                acTokenCommon = new("mayor.common1", (false, 0, false), (val, _) => val.cleared);
                acTokenAnother = new("mayor.another1", (0, true), (val, _) => val.meetings >= 3 && val.clearable);
                acTokenCommon2 = new("mayor.common2", (false, 3, false), (val, _) => val.cleared);
                acTokenChallenge = new("mayor.challenge", (false,0,false), (val, _) => val.cleared);

                if (!FixedVotesOption)
                {
                    string prefix = Language.Translate("role.mayor.hud.votes");
                    Helpers.TextHudContent("MayorText", this, (tmPro) => tmPro.text = prefix + ": " + myVote);
                }
            }
        }

        void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            if(VoteAssignmentPerTasksOption > 0 && !FixedVotesOption) UpdateMayorVotes(myVote + VoteAssignmentPerTasksOption);
        }

        [Local]
        void OnReportDeadBody(ReportDeadBodyEvent ev)
        {
            if (ev.Reporter.AmOwner && VoteAssignmentOnReportingOption > 0 && !FixedVotesOption) UpdateMayorVotes(myVote + VoteAssignmentOnReportingOption);
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            acTokenCommon!.Value.triggered = false;
            acTokenChallenge!.Value.triggered = false;
            

            if (!MyPlayer.IsDead)
            {
                if (acTokenAnother != null) acTokenAnother.Value.meetings++;

                var binder = UnityHelper.CreateObject("MayorButtons", MeetingHud.Instance.SkipVoteButton.transform.parent, MeetingHud.Instance.SkipVoteButton.transform.localPosition);
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev=> {
                    binder.gameObject.SetActive(!MyPlayer.IsDead && MeetingHud.Instance.CurrentState == MeetingHud.VoteStates.NotVoted);
                }, new GameObjectLifespan(binder));

                var countText = UnityEngine.Object.Instantiate(MeetingHud.Instance.TitleText, binder.transform);
                countText.gameObject.SetActive(true);
                countText.gameObject.GetComponent<TextTranslatorTMP>().enabled = false;
                countText.alignment = TMPro.TextAlignmentOptions.Center;
                countText.transform.localPosition = new Vector3(2.59f, 0f);
                countText.color = Palette.White;
                countText.transform.localScale *= 0.8f;
                countText.text = "";

                if(VoteAssignment > 0) UpdateMayorVotes(myVote + VoteAssignment);

                int min = Mathf.Min(MinVote, myVote);
                int max = Mathf.Min(MaxVote, myVote);
                currentVote = Mathf.Clamp(currentVote, min, max);
                countText.text = currentVote.ToString() + "/" + myVote;

                void UpdateVotes(bool increment)
                {
                    currentVote = Mathf.Clamp(currentVote + (increment ? 1 : -1), min, max);
                    countText.text = currentVote.ToString() + "/" + myVote;
                }

                if (min == max) return;

                var myArea = MeetingHud.Instance.playerStates.FirstOrDefault(v=>v.TargetPlayerId == MyPlayer.PlayerId);
                if (myArea is null) return;

                var leftRenderer = UnityHelper.CreateObject<SpriteRenderer>("MayorButton-Minus", binder.transform, new Vector3(2f, 0f));
                leftRenderer.sprite = leftButtonSprite.GetSprite();
                var leftButton = leftRenderer.gameObject.SetUpButton(true);
                leftButton.OnMouseOver.AddListener(() => leftRenderer.color = Color.gray);
                leftButton.OnMouseOut.AddListener(() => leftRenderer.color = Color.white);
                leftButton.OnClick.AddListener(() => {
                    if (myArea.DidVote) return;
                    UpdateVotes(false);
                });
                leftRenderer.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.6f);

                var rightRenderer = UnityHelper.CreateObject<SpriteRenderer>("MayorButton-Plus", binder.transform, new Vector3(3.6f, 0f));
                rightRenderer.sprite = rightButtonSprite.GetSprite();
                var rightButton = rightRenderer.gameObject.SetUpButton(true);
                rightButton.OnMouseOver.AddListener(() => rightRenderer.color = Color.gray);
                rightButton.OnMouseOut.AddListener(() => rightRenderer.color = Color.white);
                rightButton.OnClick.AddListener(() => {
                    if (myArea.DidVote) return;
                    UpdateVotes(true);
                });
                rightRenderer.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.6f);
            }
        }

        [OnlyMyPlayer]
        void OnCastVoteLocal(PlayerVoteCastLocalEvent ev)
        {
            if (!IsUsurped)
            {
                ev.Vote = currentVote;
                if (acTokenCommon != null) acTokenCommon.Value.triggered = currentVote >= 2;
            }

            if (acTokenAnother != null) acTokenAnother.Value.clearable &= currentVote == 0;
        }

        [Local]
        void OnEndVoting(MeetingVoteEndEvent ev)
        {
            if (MeetingHud.Instance.playerStates.FirstOrDefault(v => v.TargetPlayerId == MyPlayer.PlayerId)?.DidVote ?? false)
            {
                UpdateMayorVotes(myVote - currentVote);
                StatsVotes.Progress(currentVote);
                if(acTokenCommon2 != null)
                {
                    if(acTokenCommon2.Value.leftMeeting > 0)
                    {
                        if (currentVote != 1)
                            acTokenCommon2.Value.leftMeeting = 0;
                        else
                        {
                            acTokenCommon2.Value.leftMeeting--;
                            if (acTokenCommon2.Value.leftMeeting == 0) acTokenCommon2.Value.triggered = true;
                        }

                    }
                }
                Debug.Log($"Mayor: after:{myVote}");
            }
        }

        [Local]
        void OnDiscloseVotingLocal(MeetingVoteDisclosedEvent ev)
        {
            var myVote = ev.VoteStates.FirstOrDefault(v => v.VoterId == MyPlayer.PlayerId);
            if (myVote.VoterId != MyPlayer.PlayerId) return;

            var myVotedFor = myVote.VotedForId;
            acTokenCommon!.Value.myVotedFor = myVotedFor;
            acTokenChallenge!.Value.myVotedFor = myVotedFor;

            if (!ev.VoteStates.Any(v => v.VoterId != MyPlayer.PlayerId && v.VotedForId == myVotedFor))
            {
                if (acTokenChallenge != null) acTokenChallenge.Value.triggered = true;
                if (acTokenCommon2 != null) acTokenCommon2.Value.cleared |= acTokenCommon2.Value.triggered;
            }
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (acTokenCommon!.Value.triggered)
            {
                acTokenCommon.Value.cleared |= ev.Player.PlayerId == acTokenCommon.Value.myVotedFor;
            }

            if (acTokenChallenge!.Value.triggered && ev.Player.PlayerId == acTokenChallenge.Value.myVotedFor)
            {
                var team = ev.Player?.Role.Role.Team;
                if (team != NebulaTeams.JesterTeam && team != NebulaTeams.CrewmateTeam)
                {
                    acTokenChallenge.Value.cleared = true;
                }
            }
        }

        private void UpdateMayorVotes(int vote)
        {
            myVote = Mathf.Clamp(vote, 0, VotesStock);
            RpcUpdateVotes.Invoke((MyPlayer, myVote));
        }
        static private RemoteProcess<(GamePlayer player, int vote)> RpcUpdateVotes = new("UpdateMayorVotes", (message, _) =>
        {
            if (message.player.TryGetAbility<Ability>(out var mayor)) mayor.myVote = message.vote;
        });
    }
}

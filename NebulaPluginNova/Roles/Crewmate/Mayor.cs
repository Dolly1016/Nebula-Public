using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Mayor : ConfigurableStandardRole, HasCitation
{
    static public Mayor MyRole = new Mayor();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "mayor";
    public override Color RoleColor => new Color(30f / 255f, 96f / 255f, 85f / 255f);
    Citation? HasCitation.Citaion => Citations.TownOfImpostors;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player,arguments);

    private NebulaConfiguration MinVoteOption = null!;
    private NebulaConfiguration MaxVoteOption = null!;
    private NebulaConfiguration MaxVoteStockOption = null!;
    private NebulaConfiguration VoteAssignmentOption = null!;
    private NebulaConfiguration FixedVotesOption = null!;

    private int MinVote => FixedVotesOption ? VoteAssignmentOption : MinVoteOption;
    private int MaxVote => FixedVotesOption ? VoteAssignmentOption : MaxVoteOption;
    private int VoteAssignment => VoteAssignmentOption;
    private int VotesStock => FixedVotesOption ? VoteAssignmentOption : MaxVoteStockOption;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        FixedVotesOption = new(RoleConfig, "fixedVotes", null, false, false);
        MinVoteOption = new(RoleConfig, "minVote", null, 0, 20, 1, 1) { Predicate = () => !FixedVotesOption };
        MaxVoteOption = new(RoleConfig, "maxVote", null, 0, 20, 2, 2) { Predicate = () => !FixedVotesOption };
        MaxVoteStockOption = new(RoleConfig, "maxVotesStock", null, 1, 20, 8, 8) { Predicate = () => !FixedVotesOption };
        VoteAssignmentOption = new(RoleConfig, "voteAssignment", null, 1, 20, 1, 1);
    }

    public class Instance : Crewmate.Instance, IBindPlayer
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if(arguments.Length >= 1) myVote = arguments[0];
        }

        public override int[]? GetRoleArgument() => new int[] { myVote };

        static private SpriteLoader leftButtonSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingButtonLeft.png", 100f);
        static private SpriteLoader rightButtonSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingButtonRight.png", 100f);

        private int myVote = 0;
        private int currentVote = 0;

        AchievementToken<(bool cleared, byte myVotedFor, bool triggered)>? acTokenCommon = null;
        AchievementToken<(int meetings, bool clearable)>? acTokenAnother = null;
        AchievementToken<(bool cleared, byte myVotedFor, bool triggered)>? acTokenChallenge = null;

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenCommon = new("mayor.common1", (false, 0, false), (val, _) => val.cleared);
                acTokenAnother = new("mayor.another1", (0, true), (val, _) => val.meetings >= 3 && val.clearable);
                acTokenChallenge = new("mayor.challenge", (false,0,false), (val, _) => val.cleared);
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            acTokenCommon!.Value.triggered = false;
            acTokenChallenge!.Value.triggered = false;
            

            if (!MyPlayer.IsDead)
            {
                if (acTokenAnother != null) acTokenAnother.Value.meetings++;

                var countText = UnityEngine.Object.Instantiate(MeetingHud.Instance.TitleText, MeetingHud.Instance.SkipVoteButton.transform);
                countText.gameObject.SetActive(true);
                countText.gameObject.GetComponent<TextTranslatorTMP>().enabled = false;
                countText.alignment = TMPro.TextAlignmentOptions.Center;
                countText.transform.localPosition = new Vector3(2.59f, 0f);
                countText.color = Palette.White;
                countText.transform.localScale *= 0.8f;
                countText.text = "";

                myVote = Mathf.Min(myVote + MyRole.VoteAssignment, MyRole.VotesStock);
                int min = Mathf.Min(MyRole.MinVote, myVote);
                int max = Mathf.Min(MyRole.MaxVote, myVote);
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

                var leftRenderer = UnityHelper.CreateObject<SpriteRenderer>("MayorButton-Minus", MeetingHud.Instance.SkipVoteButton.transform, new Vector3(2f, 0f));
                leftRenderer.sprite = leftButtonSprite.GetSprite();
                var leftButton = leftRenderer.gameObject.SetUpButton(true);
                leftButton.OnMouseOver.AddListener(() => leftRenderer.color = Color.gray);
                leftButton.OnMouseOut.AddListener(() => leftRenderer.color = Color.white);
                leftButton.OnClick.AddListener(() => {
                    if (myArea.DidVote) return;
                    UpdateVotes(false);
                });
                leftRenderer.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.6f);

                var rightRenderer = UnityHelper.CreateObject<SpriteRenderer>("MayorButton-Plus", MeetingHud.Instance.SkipVoteButton.transform, new Vector3(3.6f, 0f));
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
            ev.Vote = currentVote;

            if (acTokenAnother != null) acTokenAnother.Value.clearable &= currentVote == 0;
            if (acTokenCommon != null) acTokenCommon.Value.triggered = currentVote >= 2;
        }

        void OnEndVoting(MeetingVoteEndEvent ev)
        {
            if (MeetingHud.Instance.playerStates.FirstOrDefault(v => v.TargetPlayerId == MyPlayer.PlayerId)?.DidVote ?? false) myVote -= currentVote;
        }

        [Local]
        void OnDiscloseVotingLocal(MeetingVoteDisclosedEvent ev)
        {
            var myVote = ev.VoteStates.FirstOrDefault(v => v.VoterId == MyPlayer.PlayerId);
            if (myVote.VoterId != MyPlayer.PlayerId) return;

            var myVotedFor = myVote.VotedForId;
            acTokenCommon!.Value.myVotedFor = myVotedFor;
            acTokenChallenge!.Value.myVotedFor = myVotedFor;

            if (!ev.VoteStates.Any(v => v.VoterId != MyPlayer.PlayerId && v.VotedForId == myVotedFor)) acTokenChallenge!.Value.triggered = true;
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
    }
}

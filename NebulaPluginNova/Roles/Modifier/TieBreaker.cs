using Virial;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Modifier;



public class TieBreaker : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, HasCitation
{
    private TieBreaker(): base("tieBreaker", "TBR", new(239, 175, 135)) { }
    Citation? HasCitation.Citation => Citations.TheOtherRoles;

    static public TieBreaker MyRole = new TieBreaker();
    static internal GameStatsEntry StatsTieBreaking = NebulaAPI.CreateStatsEntry("stats.tieBreaker.tieBreaking", GameStatsCategory.Roles, MyRole);
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public Instance(GamePlayer player) : base(player)
        {
        }


        void RuntimeAssignable.OnActivated()
        {
        }

        GamePlayer? lastVotedHost = null;
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            lastVotedHost = null;
        }

        void OnTieVotes(MeetingTieVoteHostEvent ev)
        {
            if (ev.TryCheckVotedFor(MyPlayer, out var votedFor))
            {
                ev.AddExtraVote(votedFor);
                lastVotedHost = votedFor;
                NebulaAchievementManager.RpcClearAchievement.Invoke(("tieBreaker.common1", MyPlayer));
            }
        }

        void OnVoteDisclosed(PlayerExiledEvent ev)
        {
            if (ev.Player == lastVotedHost) NebulaAchievementManager.RpcClearAchievement.Invoke(("tieBreaker.challenge", MyPlayer));
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += " ♠".Color(MyRole.UnityColor);
        }
    }
}

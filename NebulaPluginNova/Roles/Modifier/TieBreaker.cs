using Virial;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
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


        AchievementToken<bool>? acTokenCommon;
        AchievementToken<(bool cleared, byte lastTieVoted)>? acTokenChallenge;

        public Instance(GamePlayer player) : base(player)
        {
        }


        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                acTokenCommon = new("tieBreaker.common1", false, (val, _) => val);
                acTokenChallenge = new("tieBreaker.challenge", (false, 255), (val, _) => val.cleared);
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (acTokenChallenge != null) acTokenChallenge.Value.lastTieVoted = 255;
        }

        static public RemoteProcess<(byte playerId, byte votedFor)> RpcNoticeTieBreak = new("NoticeTieBreak", (message, _) => {
            if(message.playerId == PlayerControl.LocalPlayer.PlayerId && (NebulaGameManager.Instance?.GetPlayer(message.playerId)?.TryGetModifier<Instance>(out var role) ?? false)) { 
                if (role.acTokenCommon != null) role.acTokenCommon.Value = true;
                if (role.acTokenChallenge != null) role.acTokenChallenge.Value.lastTieVoted = message.votedFor;
                StatsTieBreaking.Progress();
            }
        });

        void OnTieVotes(MeetingTieVoteHostEvent ev)
        {
            if (ev.TryCheckVotedFor(MyPlayer, out var votedFor)) ev.AddExtraVote(votedFor);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += " ♠".Color(MyRole.UnityColor);
        }
    }
}

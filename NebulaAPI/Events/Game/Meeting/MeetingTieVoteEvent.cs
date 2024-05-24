using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

public class MeetingTieVoteEvent : Event
{
    private Dictionary<byte, Virial.Game.Player?> voteForMap;
    internal List<Virial.Game.Player?> ExtraVotes { get; private init; } = new();

    internal MeetingTieVoteEvent(Dictionary<byte, Virial.Game.Player?> voteForMap)
    {
        this.voteForMap = voteForMap;
    }

    internal bool TryCheckVotedFor(byte voterId, out Virial.Game.Player? votedFor) { 
        return voteForMap.TryGetValue(voterId, out votedFor);
    }

    public bool TryCheckVotedFor(Virial.Game.Player voter, out Virial.Game.Player? votedFor) => TryCheckVotedFor(voter.PlayerId, out votedFor);

    public void AddExtraVote(Virial.Game.Player? voteFor) => ExtraVotes.Add(voteFor);
}

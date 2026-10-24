using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

public class MeetingSetUpVoteAreaEvent : Event
{
    internal IEnumerable<PlayerVoteArea> VoteAreas { get; private init; }
    internal MeetingSetUpVoteAreaEvent(IEnumerable<PlayerVoteArea> voteAreas)
    {
        VoteAreas = voteAreas;
    }
}

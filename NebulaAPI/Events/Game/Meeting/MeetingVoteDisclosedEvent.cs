using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

internal class MeetingVoteDisclosedEvent : Event
{
    public IEnumerable<MeetingHud.VoterState> VoteStates { get { foreach (var v in states) yield return v; } }
    private MeetingHud.VoterState[] states;

    internal MeetingVoteDisclosedEvent(MeetingHud.VoterState[] states)
    {
        this.states = states;
    }
}

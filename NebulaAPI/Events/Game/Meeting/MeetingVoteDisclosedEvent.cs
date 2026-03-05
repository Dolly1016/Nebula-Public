using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

internal class MeetingVoteDisclosedEvent : Event
{
    internal IEnumerable<MeetingHud.VoterState> VoteStates { get { foreach (var v in states) yield return v; } }
    internal IEnumerable<MeetingHud.VoterState> PreMappedVoteStates { get { foreach (var v in preMapStates) yield return v; } }

    private readonly MeetingHud.VoterState[] preMapStates;
    private MeetingHud.VoterState[] states;

    internal MeetingVoteDisclosedEvent(MeetingHud.VoterState[] preMapStates, MeetingHud.VoterState[] states)
    {
        this.preMapStates = preMapStates;
        this.states = states;
    }
}

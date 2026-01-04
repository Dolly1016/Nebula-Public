using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 投票終了時に発火します。
/// </summary>
public class MeetingVoteEndEvent : Event
{
    private MeetingHud.VoterState[] voteStates;
    internal MeetingHud.VoterState[] VoteStates => voteStates;
    internal MeetingVoteEndEvent(MeetingHud.VoterState[] states)
    {
        this.voteStates = states;
    } 
}

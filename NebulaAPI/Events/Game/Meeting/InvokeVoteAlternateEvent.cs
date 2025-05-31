using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

internal class InvokeVoteAlternateEvent : AbstractPlayerEvent
{
    internal PlayerVoteArea VoteArea { get; private set; }
    internal InvokeVoteAlternateEvent(PlayerVoteArea voteArea, Virial.Game.Player player) : base(player)
    {
        VoteArea = voteArea;
    }
}

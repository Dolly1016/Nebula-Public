using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;
using Virial.Game;

namespace Virial.Events.Game.Meeting;

internal class SetUpVotingAreaEvent : AbstractPlayerEvent
{
    internal PlayerVoteArea VoteArea { get; }
    internal SetUpVotingAreaEvent(PlayerVoteArea voteArea, Virial.Game.Player player) : base(player)
    {
        VoteArea = voteArea;
    }
}

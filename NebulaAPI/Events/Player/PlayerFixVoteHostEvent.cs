using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerFixVoteHostEvent : AbstractPlayerEvent
{
    Virial.Game.Player? voteTo = null;
    bool didVote = false;
    int vote = 0;
    public Virial.Game.Player? VoteTo { get => voteTo; set { 
            voteTo = value;
            didVote = true;
        }
    }
    public bool DidVote { get => didVote; set { 
            didVote = value;
            if (!didVote) voteTo = null;
        } 
    }

    public int Vote
    {
        get => didVote ? vote : 0; set
        {
            vote = value;
        }
    }

    internal PlayerFixVoteHostEvent(Virial.Game.Player player, bool didVote, Virial.Game.Player? voteTo, int vote) : base(player)
    {
        this.voteTo = voteTo;
        this.didVote = didVote;
        this.vote = vote;
    }
}

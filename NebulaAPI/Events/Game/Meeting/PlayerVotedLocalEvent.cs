using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

public class PlayerVotedLocalEvent : AbstractPlayerEvent
{
    public IEnumerable<Virial.Game.Player> Voters { get { foreach (var v in voters) yield return v; } }
    private Virial.Game.Player[] voters;

    internal PlayerVotedLocalEvent(Virial.Game.Player player, Virial.Game.Player[] voters) : base(player)
    {
        this.voters = voters;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerGuardEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Murderer { get; private init; }

    internal PlayerGuardEvent(Virial.Game.Player player, Virial.Game.Player killer) : base(player)
    {
        this.Murderer = killer;
    }
}

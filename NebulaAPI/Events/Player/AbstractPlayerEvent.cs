using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class AbstractPlayerEvent : Event
{
    public Virial.Game.Player Player { get; private init; }

    internal AbstractPlayerEvent(Virial.Game.Player player)
    {
        this.Player = player;
    }
}
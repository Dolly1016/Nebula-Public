using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class AbstractPlayerEvent : Event
{
    public Game.Player Player { get; private init; }

    internal AbstractPlayerEvent(Game.Player player)
    {
        this.Player = player;
    }
}
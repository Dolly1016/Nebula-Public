using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Player;

public class PlayerMurderEvent : PlayerDeadEvent
{
    public Game.Player Killer { get; internal set; }
    internal PlayerMurderEvent(Game.Player dead, Game.Player killer) : base(dead)
    {
        Killer = killer;
    }
}

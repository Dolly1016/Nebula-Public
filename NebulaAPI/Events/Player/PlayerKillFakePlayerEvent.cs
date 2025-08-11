using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Player;

public class PlayerKillFakePlayerEvent : Event
{
    public IFakePlayer Target { get; private init; }
    public Virial.Game.Player Killer { get; private init; }

    internal PlayerKillFakePlayerEvent(IFakePlayer target, Virial.Game.Player killer)
    {
        Target = target;
        Killer = killer;
    }
}

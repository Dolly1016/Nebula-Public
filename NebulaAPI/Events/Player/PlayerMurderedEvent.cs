using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerMurderedEvent : PlayerDieEvent
{
    public Virial.Game.Player Murderer { get; private init; }
    public Virial.Game.Player Dead => Player;
    public bool WithBlink { get; private init; }

    internal PlayerMurderedEvent(Virial.Game.Player dead, Virial.Game.Player killer, bool withBlink) : base(dead)
    {
        this.Murderer = killer;
        this.WithBlink = withBlink;
    }
}

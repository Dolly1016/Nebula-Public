using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerExtraExiledEvent : PlayerDieEvent
{
    public Virial.Game.Player? Murderer { get; private init; }
    public Virial.Game.Player Dead => Player;

    internal PlayerExtraExiledEvent(Virial.Game.Player dead, Virial.Game.Player? killer) : base(dead)
    {
        this.Murderer = killer;
    }

}

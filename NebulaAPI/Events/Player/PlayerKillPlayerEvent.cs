using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerKillPlayerEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Murderer => Player;
    public Virial.Game.Player Dead { get; private init; }

    internal PlayerKillPlayerEvent(Virial.Game.Player killer, Virial.Game.Player dead) : base(killer) {
        Dead = dead;
    }
}

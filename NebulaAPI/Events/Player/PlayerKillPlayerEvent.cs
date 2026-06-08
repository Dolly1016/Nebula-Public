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
    public bool WithDeadBody { get; private init; }
    public Virial.Compat.Vector2? DeadBodyPos { get; private init; }
    internal PlayerKillPlayerEvent(Virial.Game.Player killer, Virial.Game.Player dead, bool withDeadBody, Virial.Compat.Vector2? deadBodyPos = null) : base(killer) {
        Dead = dead;
        this.WithDeadBody = withDeadBody;
        this.DeadBodyPos = deadBodyPos;
    }
}

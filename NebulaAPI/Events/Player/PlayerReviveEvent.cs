using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerReviveEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Revived => Player;
    public Virial.Game.Player? Healer { get; private init; }

    internal PlayerReviveEvent(Virial.Game.Player revived, Virial.Game.Player? healer) : base(revived)
    {
        Healer = healer;
    }
}
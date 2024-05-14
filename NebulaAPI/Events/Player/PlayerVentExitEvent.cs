using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerVentExitEvent : AbstractPlayerEvent
{
    internal Vent Vent { get; set; }

    internal PlayerVentExitEvent(Virial.Game.Player player, Vent vent) : base(player)
    {
        this.Vent = vent;
    }
}


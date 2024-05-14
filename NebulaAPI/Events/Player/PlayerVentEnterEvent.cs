using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerVentEnterEvent : AbstractPlayerEvent
{
    internal Vent Vent { get; set; }

    internal PlayerVentEnterEvent(Virial.Game.Player player, Vent vent) : base(player)
    {
        this.Vent = vent;
    }
}

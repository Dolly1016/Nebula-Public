using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerTaskUpdateEvent : AbstractPlayerEvent
{
    internal PlayerTaskUpdateEvent(Virial.Game.Player player) : base(player)
    {

    }
}

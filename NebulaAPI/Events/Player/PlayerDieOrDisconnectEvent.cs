using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerDieOrDisconnectEvent : AbstractPlayerEvent
{
    internal PlayerDieOrDisconnectEvent(Virial.Game.Player player) : base(player) { }
}

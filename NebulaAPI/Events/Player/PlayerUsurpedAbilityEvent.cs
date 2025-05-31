using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerUsurpedAbilityEvent : AbstractPlayerEvent
{

    internal PlayerUsurpedAbilityEvent(Virial.Game.Player player) : base(player)
    {
    }
}

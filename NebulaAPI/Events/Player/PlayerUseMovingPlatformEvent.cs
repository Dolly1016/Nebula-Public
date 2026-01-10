using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerUseMovingPlatformEvent : AbstractPlayerEvent
{
    public Virial.Compat.Vector2 From { get; private init; }
    public Virial.Compat.Vector2 To { get; private init; }
    internal PlayerUseMovingPlatformEvent(Virial.Game.Player player, Virial.Compat.Vector2 from, Virial.Compat.Vector2 to) : base(player)
    {
        From = from;
        To = to;
    }

}
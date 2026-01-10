using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerClimbLadderEvent : AbstractPlayerEvent
{
    public bool IsClimbingUp { get; private init; }
    public Virial.Compat.Vector2 From { get; private init; }
    public Virial.Compat.Vector2 To { get; private init; }
    internal PlayerClimbLadderEvent(Virial.Game.Player player, bool isClimbingUp, Virial.Compat.Vector2 from, Virial.Compat.Vector2 to) : base(player)
    {
        IsClimbingUp = isClimbingUp;
        From = from;
        To = to;
    }

}

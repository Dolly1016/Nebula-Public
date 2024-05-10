using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerModifierRemoveEvent : AbstractPlayerEvent
{
    public Virial.Assignable.RuntimeModifier Modifier { get; private init; }
    internal PlayerModifierRemoveEvent(Virial.Game.Player player, Virial.Assignable.RuntimeModifier modifier) : base(player)
    {
        this.Modifier = modifier;
    }
}

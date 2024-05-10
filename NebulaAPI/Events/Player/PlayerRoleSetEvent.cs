using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerRoleSetEvent : AbstractPlayerEvent
{
    public Virial.Assignable.RuntimeRole Role { get; private init; }
    internal PlayerRoleSetEvent(Virial.Game.Player player, Virial.Assignable.RuntimeRole role) : base(player) {
        this.Role = role;
    }
}

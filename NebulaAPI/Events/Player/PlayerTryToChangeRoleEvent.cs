using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerTryToChangeRoleEvent : AbstractPlayerEvent
{
    public Virial.Assignable.RuntimeRole CurrentRole { get; private init; }
    public Virial.Assignable.DefinedRole NextRole { get; private init; }
    internal PlayerTryToChangeRoleEvent(Virial.Game.Player player, Virial.Assignable.RuntimeRole currentRole, Virial.Assignable.DefinedRole nextRole) : base(player)
    {
        this.CurrentRole = currentRole;
        this.NextRole = nextRole;
    }
}
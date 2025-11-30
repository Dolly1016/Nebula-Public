using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Events.Role;

internal class PreFixAssignmentEvent : Event
{
    public IRoleTable RoleTable { get; }
    internal PreFixAssignmentEvent(IRoleTable roleTable)
    {
        this.RoleTable = roleTable;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Events.Role;

public class RoleAllocatorSetRoleEvent : Event
{
    public IRoleAllocator Allocator { get; }
    public DefinedRole Role { get; }
    private Action<DefinedRole>? ejectRole { get; }

    public void ExcludeRole(DefinedRole role) => ejectRole?.Invoke(role);

    internal RoleAllocatorSetRoleEvent(IRoleAllocator allocator, DefinedRole role, Action<DefinedRole>? ejectRole)
    {
        Allocator = allocator;
        Role = role;
        this.ejectRole = ejectRole;
    }
}


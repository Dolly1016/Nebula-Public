using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Assignable;

public interface Assignables
{
    IEnumerable<DefinedAssignable> AllAssignables { get; }
    IEnumerable<DefinedRole> AllRoles { get; }
    IEnumerable<DefinedModifier> AllModifiers { get; }
    IEnumerable<DefinedGhostRole> AllGhostRoles { get; }
    Assignable.DefinedRole? GetRole(string internalName);
    Assignable.DefinedRole? GetRoleById(int id);
    Assignable.DefinedModifier? GetModifier(string internalName);
    Assignable.DefinedModifier? GetModifierById(int id);
    Assignable.DefinedGhostRole? GetGhostRole(string internalName);
    Assignable.DefinedGhostRole? GetGhostRoleById(int id);

}

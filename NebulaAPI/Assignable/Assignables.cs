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
    /// <summary>
    /// 指定の人数、あるいは現在の人数で役職割り当てをシミュレートします。
    /// </summary>
    /// <param name="players"></param>
    /// <returns></returns>
    AssignmentSummary CalcAssignmentSummary(int? players = null);
}

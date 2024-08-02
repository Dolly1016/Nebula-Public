using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Configuration;

public interface AssignableFilter<T> where T : DefinedAssignable {
    bool Test(T assignable);
    void ToggleAndShare(T assignable);
    void SetAndShare(T assignable, bool val);
}

/// <summary>
/// 割り当て可能なモディファイアに制限をかけます。
/// </summary>
public interface ModifierFilter : AssignableFilter<DefinedModifier>
{
}

/// <summary>
/// 割り当て可能な幽霊役職に制限をかけます。
/// </summary>
public interface GhostRoleFilter : AssignableFilter<DefinedGhostRole>
{
}

/// <summary>
/// 割り当て可能な役職に制限をかけます。
/// </summary>
public interface RoleFilter : AssignableFilter<DefinedRole>
{
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Configuration;

/// <summary>
/// 割り当て可能なモディファイアに制限をかけます。
/// </summary>
public interface ModifierFilter
{
    bool Test(DefinedModifier modifier);
}

/// <summary>
/// 割り当て可能な幽霊役職に制限をかけます。
/// </summary>
public interface GhostRoleFilter
{
    bool Test(DefinedGhostRole modifier);
}

/// <summary>
/// 割り当て可能な役職に制限をかけます。
/// </summary>
public interface RoleFilter
{
    bool Test(DefinedRole role);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Assignable;

/// <summary>
/// 付随割り当てを表します。
/// </summary>
/// <param name="Role"></param>
/// <param name="Reason"></param>
public record AdditionalAssignment(DefinedRole Role, DefinedRole Reason);

/// <summary>
/// 確率的な割り当て(100%割り当て含む)を表します。
/// </summary>
/// <param name="Role"></param>
/// <param name="AssignmentType"></param>
/// <param name="Count"></param>
/// <param name="Chance"></param>
/// <param name="SecondaryCount"></param>
/// <param name="SecondaryChance"></param>
public record ProbabilityAssignment(DefinedRole Role, AssignmentType? AssignmentType, int Count, int Chance, int? SecondaryCount, int? SecondaryChance);

/// <summary>
/// カテゴリごとのモディファイア系割り当てを表します。
/// </summary>
/// <param name="Assignable"></param>
/// <param name="Category"></param>
/// <param name="Count"></param>
/// <param name="Chance"></param>
/// <param name="SecondaryCount"></param>
/// <param name="SecondaryChance"></param>
public record ModifierlikeAssignment<Modifierlike>(Modifierlike Assignable, RoleCategory Category, int Count, int Chance, int? SecondaryCount, int? SecondaryChance) where Modifierlike : IAssignToCategorizedRole;

/// <summary>
/// 特殊な割り当てを表します。
/// </summary>
/// <param name="Role"></param>
/// <param name="Count"></param>
/// <param name="Chance"></param>
/// <param name="SecondaryCount"></param>
/// <param name="SecondaryChance"></param>
public record SpecialAssignment(DefinedAssignable Role, int Count, int Chance, int? SecondaryCount, int? SecondaryChance);

/// <summary>
/// 割り当てプレビューです。
/// </summary>
/// <param name="Roles"></param>
/// <param name="Modifiers"></param>
/// <param name="GhostRoles"></param>
/// <param name="Specials"></param>
/// <param name="Additionals"></param>
public record AssignmentSummary(IReadOnlyList<ProbabilityAssignment> Roles, IReadOnlyList<ModifierlikeAssignment<DefinedAllocatableModifier>> Modifiers, IReadOnlyList<ModifierlikeAssignment<DefinedGhostRole>> GhostRoles, IReadOnlyList<SpecialAssignment> Specials, IReadOnlyList<AdditionalAssignment> Additionals)
{
    /// <summary>
    /// 役職が出現するか調べます。
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    public bool IsSpawnable(DefinedRole role) => Roles.Any(assignment => assignment.Role == role);
    /// <summary>
    /// モディファイアが出現するか調べます。
    /// </summary>
    /// <param name="modifier"></param>
    /// <returns></returns>
    public bool IsSpawnable(DefinedAllocatableModifier modifier) => Modifiers.Any(assignment => assignment.Assignable == modifier);
    /// <summary>
    /// モディファイアが指定のカテゴリで出現するか調べます。
    /// </summary>
    /// <param name="modifier"></param>
    /// <param name="category"></param>
    /// <returns></returns>
    public bool IsSpawnable(DefinedAllocatableModifier modifier, RoleCategory category) => Modifiers.Any(assignment => assignment.Assignable == modifier && assignment.Category == category);
    /// <summary>
    /// 幽霊役職が出現するか調べます。
    /// </summary>
    /// <param name="ghostRole"></param>
    /// <returns></returns>
    public bool IsSpawnable(DefinedGhostRole ghostRole) => GhostRoles.Any(assignment => assignment.Assignable == ghostRole);
    /// <summary>
    /// 幽霊役職が指定のカテゴリで出現するか調べます。
    /// </summary>
    /// <param name="ghostRole"></param>
    /// <param name="category"></param>
    /// <returns></returns>
    public bool IsSpawnable(DefinedGhostRole ghostRole, RoleCategory category) => GhostRoles.Any(assignment => assignment.Assignable == ghostRole && assignment.Category == category);

}

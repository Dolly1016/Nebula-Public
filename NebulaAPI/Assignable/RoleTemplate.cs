using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;
using Virial.Game;

namespace Virial.Assignable;

/// <summary>
/// 役職定義のテンプレートです。
/// </summary>
public class DefinedAssignableTemplate : DefinedAssignable
{
    internal protected IConfigurationHolder? ConfigurationHolder { get; internal set; }
    IConfigurationHolder? DefinedAssignable.ConfigurationHolder => ConfigurationHolder;

    protected string LocalizedName { get; private init; }
    string DefinedAssignable.LocalizedName => LocalizedName;

    protected Virial.Color RoleColor { get; private init; }
    internal UnityEngine.Color UnityColor { get; private init; }

    Color DefinedAssignable.Color => RoleColor;
    UnityEngine.Color DefinedAssignable.UnityColor => UnityColor;
    int IRoleID.Id { get; set; } = -1;

    public DefinedAssignableTemplate(string localizedName, Virial.Color color)
    {
        ConfigurationHolder = null;
        LocalizedName = localizedName;
        RoleColor = color;
        UnityColor = new(color.R, color.G, color.B, 1f);
    }
}

public class DefinedSingleAssignableTemplate : DefinedAssignableTemplate, DefinedSingleAssignable
{
    private class StandardAssignmentParameters : AllocationParameters
    {
        private IntegerConfiguration roleCountOption;
        private IntegerConfiguration roleChanceOption;
        private ISharableVariable<int>? roleSecondaryChanceOption;

        IEnumerable<IConfiguration> AllocationParameters.Configurations => [roleCountOption, roleChanceOption];
        int AllocationParameters.RoleCount => throw new NotImplementedException();

        int AllocationParameters.GetRoleChance(int count)
        {
            if (roleSecondaryChanceOption == null || count == 1)
                return roleChanceOption.GetValue();
            else
                return roleSecondaryChanceOption.CurrentValue;
        }
    }

    private StandardAssignmentParameters? myAssignmentParameters = null;
    AllocationParameters DefinedSingleAssignable.AllocationParameters => myAssignmentParameters;

    protected RoleCategory Category { get; private init; }
    RoleCategory DefinedSingleAssignable.Category => Category;

    protected RoleTeam Team { get; private init; }
    RoleTeam DefinedSingleAssignable.Team => Team;

    public DefinedSingleAssignableTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, bool withAssignmentOption = true) : base(localizedName, color)
    {
        this.Category = category;
        this.Team = team;

        if (withAssignmentOption)
        {

        }
    }
}

public class DefinedRoleTemplate : DefinedSingleAssignableTemplate, IGuessed, AssignableFilterHolder
{

    ISharableVariable<bool>? IGuessed.CanBeGuessVariable { get; set; } = null;

    private ModifierFilter modifierFilter;
    private GhostRoleFilter? ghostRoleFilter;
    ModifierFilter? AssignableFilterHolder.ModifierFilter => modifierFilter;

    GhostRoleFilter? AssignableFilterHolder.GhostRoleFilter => ghostRoleFilter;

    /// <summary>
    /// デフォルト設定で幽霊役職/モディファイアを割り当てられるかどうか返します。
    /// </summary>
    /// <param name="modifier"></param>
    /// <returns></returns>
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable)
    {
        if (assignable is AllocatableDefinedModifier or DefinedGhostRole)
            return true;
        return false;
    }

    /// <summary>
    /// 幽霊役職/モディファイアを割り当てられるかどうか返します。
    /// </summary>
    /// <param name="assignable"></param>
    /// <returns></returns>
    bool AssignableFilterHolder.CanLoad(DefinedAssignable assignable)
    {
        AssignableFilterHolder filterHolder = this as AssignableFilterHolder;
        if (!filterHolder.CanLoadDefault(assignable)) return false;

        if (assignable is DefinedModifier dm)
            return filterHolder.ModifierFilter?.Test(dm) ?? true;
        if (assignable is DefinedGhostRole dg)
            return filterHolder.GhostRoleFilter?.Test(dg) ?? true;

        return false;
    }

    public DefinedRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, bool withAssignmentOption = true, bool withOptionHolder = true) : base(localizedName, color, category, team, withAssignmentOption)
    {
        if ((this as IGuessed).CanBeGuessDefault)
            (this as IGuessed).CanBeGuessVariable = NebulaAPI.Configurations.BoolVariable("role." + LocalizedName + ".canBeGuess", true);

        modifierFilter = NebulaAPI.Configurations.ModifierFilter("role." + LocalizedName + ".modifierFilter");
        ghostRoleFilter = NebulaAPI.Configurations.GhostRoleFilter("role." + LocalizedName + ".ghostRoleFilter");

        if (withOptionHolder)
        {
            TryGenerateOptionHolder();
            if (withAssignmentOption) ConfigurationHolder.AppendConfigurations((this as DefinedSingleAssignable).AllocationParameters.Configurations);
        }
    }

    /// <summary>
    /// オプションホルダを生成します。
    /// すでに生成済みの場合は何もしません。
    /// </summary>
    /// <returns></returns>
    protected bool TryGenerateOptionHolder()
    {
        if (ConfigurationHolder != null) return false;
        ConfigurationHolder = NebulaAPI.Configurations.Holder("role." + LocalizedName, [Category], GameModes.AllGameModes);
        return true;
    }

}
public class DefinedAllocatableModifierTemplate : DefinedAssignableTemplate, RoleFilter, HasRoleFilter
{
    bool RoleFilter.Test(DefinedRole role) => role.ModifierFilter?.Test((this as DefinedModifier)!) ?? false;
    RoleFilter HasRoleFilter.RoleFilter => this;

    public DefinedAllocatableModifierTemplate(string localizedName, Virial.Color color) : base(localizedName, color)
    {
    }
}

public class DefinedGhostRoleTemplate : DefinedSingleAssignableTemplate, RoleFilter, HasRoleFilter
{
    bool RoleFilter.Test(DefinedRole role) => role.GhostRoleFilter?.Test((this as DefinedGhostRole)!) ?? false;
    RoleFilter HasRoleFilter.RoleFilter => this;

    public DefinedGhostRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team) : base(localizedName, color, category, team)
    {
    }
}

/// <summary>
/// 実行時役職のテンプレートです。
/// </summary>
public class RuntimeAssignableTemplate : ComponentHolder, IBindPlayer
{
    protected Virial.Game.Player MyPlayer { get; private init; }
    protected bool AmOwner => MyPlayer.AmOwner;
    Virial.Game.Player IBindPlayer.MyPlayer => MyPlayer;

    public RuntimeAssignableTemplate(Player myPlayer)
    {
        MyPlayer = myPlayer;
    }
}

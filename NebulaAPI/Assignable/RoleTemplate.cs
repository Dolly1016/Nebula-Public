using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;
using static Rewired.UI.ControlMapper.ControlMapper;

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
        private IOrderedSharableVariable<int> roleChanceEntry;
        private IOrderedSharableVariable<int>? roleSecondaryChanceEntry;
        private IConfiguration roleChanceEditor;

        public StandardAssignmentParameters(string id, bool isImpostor)
        {
            roleCountOption = NebulaAPI.Configurations.Configuration(id + ".count", ArrayHelper.Selection(0, isImpostor ? 5 : 15), 0);

            roleChanceEntry = NebulaAPI.Configurations.SharableVariable(id + ".chance", ArrayHelper.Selection(10, 100, 10), 100);
            roleSecondaryChanceEntry = NebulaAPI.Configurations.SharableVariable(id + ".secondaryChance", ArrayHelper.Selection(0, 100, 10), 0);

            roleChanceEditor = NebulaAPI.Configurations.Configuration(
                () => "",
                () => {
                    var gui = NebulaAPI.GUI;
                    if (roleCountOption.GetValue() <= 1)
                    {
                        return gui.HorizontalHolder(Media.GUIAlignment.Center,
                        gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), "options.role.chance"),
                        gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.OptionsButton), "<<", _ => { roleChanceEntry.ChangeValue(false, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), roleChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage")),
                        gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.OptionsButton), ">>", _ => { roleChanceEntry.ChangeValue(true, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
                        );
                    }
                    else
                    {
                        return gui.HorizontalHolder(Media.GUIAlignment.Center,
                        gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), "options.role.chance"),
                        gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.OptionsButton), "<<", _ => { roleChanceEntry.ChangeValue(false, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), roleChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage")),
                        gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.OptionsButton), ">>", _ => { roleChanceEntry.ChangeValue(true, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                        gui.HorizontalMargin(0.3f),
                        gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), "options.role.secondaryChance"),
                        gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.OptionsButton), "<<", _ => { roleSecondaryChanceEntry.ChangeValue(false, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), roleSecondaryChanceEntry.CurrentValue > 0 ? (roleSecondaryChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage")) : "-"),
                        gui.RawButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.OptionsButton), ">>", _ => { roleSecondaryChanceEntry.ChangeValue(true, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
                        );
                    }
                },
                () => roleCountOption.GetValue() > 0);
        }

        IEnumerable<IConfiguration> AllocationParameters.Configurations => [roleCountOption, roleChanceEditor];
        int AllocationParameters.RoleCount => roleCountOption.GetValue();

        int AllocationParameters.GetRoleChance(int count)
        {
            if (roleSecondaryChanceEntry == null || roleSecondaryChanceEntry.CurrentValue == 0 || count == 1)
                return roleChanceEntry.CurrentValue;
            else
                return roleSecondaryChanceEntry.CurrentValue;
        }
    }

    private StandardAssignmentParameters? myAssignmentParameters = null;
    AllocationParameters? DefinedSingleAssignable.AllocationParameters => myAssignmentParameters;

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
            myAssignmentParameters = new("role." + (this as DefinedAssignable).InternalName, category == RoleCategory.ImpostorRole);
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

    public DefinedRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, IEnumerable<IConfiguration>? configurations = null, bool withAssignmentOption = true, bool withOptionHolder = true) : base(localizedName, color, category, team, withAssignmentOption)
    {
        if ((this as IGuessed).CanBeGuessDefault)
            (this as IGuessed).CanBeGuessVariable = NebulaAPI.Configurations.SharableVariable("role." + (this as DefinedAssignable).InternalName + ".canBeGuess", true);

        modifierFilter = NebulaAPI.Configurations.ModifierFilter("role." + (this as DefinedAssignable).InternalName + ".modifierFilter");
        ghostRoleFilter = NebulaAPI.Configurations.GhostRoleFilter("role." + (this as DefinedAssignable).InternalName + ".ghostRoleFilter");

        if (withOptionHolder)
        {
            TryGenerateOptionHolder();
            if (withAssignmentOption) ConfigurationHolder!.AppendConfigurations((this as DefinedSingleAssignable).AllocationParameters!.Configurations);

            if (configurations != null) ConfigurationHolder!.AppendConfigurations(configurations);
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
        ConfigurationHolder = NebulaAPI.Configurations.Holder("role." + (this as DefinedAssignable).InternalName, [Category], GameModes.AllGameModes);
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

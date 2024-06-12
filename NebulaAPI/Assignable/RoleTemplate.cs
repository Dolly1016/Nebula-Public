using Il2CppSystem.Linq.Expressions.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Configuration;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;
using static Rewired.UI.ControlMapper.ControlMapper;
using static Unity.Profiling.ProfilerRecorder;

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

    public DefinedAssignableTemplate(string localizedName, Virial.Color color, ConfigurationTab? tab = null, Func<bool>? optionHolderPredicate = null, Func<ConfigurationHolderState>? optionHolderState = null)
    {
        ConfigurationHolder = null;
        LocalizedName = localizedName;
        RoleColor = color;
        UnityColor = new(color.R, color.G, color.B, 1f);
        NebulaAPI.Preprocessor?.RegisterAssignable(this);

        if(tab != null)
            ConfigurationHolder = NebulaAPI.Configurations.Holder(NebulaAPI.GUI.TextComponent(this.RoleColor, "role." + this.LocalizedName + ".name"), NebulaAPI.GUI.LocalizedTextComponent("options.role." + this.LocalizedName + ".detail"), [tab], GameModes.AllGameModes, optionHolderPredicate, optionHolderState);
        
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
            roleCountOption = NebulaAPI.Configurations.Configuration(id + ".count", (0, isImpostor ? 5 : 15), 0, title: NebulaAPI.GUI.LocalizedTextComponent("options.role.count"));

            roleChanceEntry = NebulaAPI.Configurations.SharableVariable(id + ".chance", (10, 100, 10), 100);
            roleSecondaryChanceEntry = NebulaAPI.Configurations.SharableVariable(id + ".secondaryChance", (0, 100, 10), 0);

            roleChanceEditor = NebulaAPI.Configurations.Configuration(
                () =>
                {
                    string str = NebulaAPI.Language.Translate("options.role.chance") + ": " + roleChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage");
                    if (roleCountOption.GetValue() > 1 && roleSecondaryChanceEntry.CurrentValue > 0) str += (" (" + roleSecondaryChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage") + ")").Color(UnityEngine.Color.gray);
                    return str;
                },
                () => {
                    var gui = NebulaAPI.GUI;
                    if (roleCountOption.GetValue() <= 1)
                    {
                        return gui.HorizontalHolder(Media.GUIAlignment.Left,
                        gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), "options.role.chance"),
                        gui.HorizontalMargin(0.1f),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                        gui.HorizontalMargin(0.1f),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), roleChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage")),
                        gui.SpinButton(GUIAlignment.Center, v => { roleChanceEntry.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
                        );
                    }
                    else
                    {
                        return gui.HorizontalHolder(Media.GUIAlignment.Left,
                        gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), "options.role.chance"),
                        gui.HorizontalMargin(0.1f),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                        gui.HorizontalMargin(0.1f),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), roleChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage")),
                        gui.SpinButton(GUIAlignment.Center, v => { roleChanceEntry.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                        gui.HorizontalMargin(0.3f),
                        gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), "options.role.secondaryChance"),
                        gui.HorizontalMargin(0.1f),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                        gui.HorizontalMargin(0.1f),
                        gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), roleSecondaryChanceEntry.CurrentValue > 0 ? (roleSecondaryChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage")) : "-"),
                        gui.SpinButton(GUIAlignment.Center, v => { roleSecondaryChanceEntry.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); })
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

    private AllocationParameters? myAssignmentParameters = null;
    AllocationParameters? DefinedSingleAssignable.AllocationParameters => myAssignmentParameters;

    protected RoleCategory Category { get; private init; }
    RoleCategory DefinedSingleAssignable.Category => Category;

    protected RoleTeam Team { get; private init; }
    RoleTeam DefinedSingleAssignable.Team => Team;

    public DefinedSingleAssignableTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, bool withAssignmentOption = true, ConfigurationTab? tab = null, Func<bool>? optionHolderPredicate = null) : base(localizedName, color, tab, optionHolderPredicate)
    {
        this.Category = category;
        this.Team = team;

        if (withAssignmentOption)
        {
            myAssignmentParameters = new StandardAssignmentParameters("role." + (this as DefinedAssignable).InternalName, category == RoleCategory.ImpostorRole);
            ConfigurationHolder?.AppendConfigurations(myAssignmentParameters.Configurations);

            ConfigurationHolder?.SetDisplayState(() => myAssignmentParameters.RoleCount == 0 ? ConfigurationHolderState.Inactivated : myAssignmentParameters.GetRoleChance(1) == 100 ? ConfigurationHolderState.Emphasized : ConfigurationHolderState.Activated);
        }
    }

    bool DefinedSingleAssignable.IsSpawnable => (myAssignmentParameters?.RoleCount ?? 0) > 0;
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
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable);

    protected bool CanLoadDefaultTemplate(DefinedAssignable assignable)
    {
        if (assignable is DefinedAllocatableModifierTemplate damt) return damt.CanAssignTo(Category);
        if (assignable is DefinedGhostRoleTemplate dgrt) return (dgrt as DefinedSingleAssignable).Category == Category;
        else if (assignable is DefinedAllocatableModifier or DefinedGhostRole) return true;
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

    public DefinedRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, IEnumerable<IConfiguration>? configurations = null, bool withAssignmentOption = true, bool withOptionHolder = true, Func<bool>? optionHolderPredicate = null) : base(localizedName, color, category, team, withAssignmentOption, withOptionHolder ? category : null, optionHolderPredicate)
    {
        if ((this as IGuessed).CanBeGuessDefault)
            (this as IGuessed).CanBeGuessVariable = NebulaAPI.Configurations.SharableVariable("role." + (this as DefinedAssignable).InternalName + ".canBeGuess", true);

        modifierFilter = NebulaAPI.Configurations.ModifierFilter("role." + (this as DefinedAssignable).InternalName + ".modifierFilter");
        ghostRoleFilter = NebulaAPI.Configurations.GhostRoleFilter("role." + (this as DefinedAssignable).InternalName + ".ghostRoleFilter");

        if (withOptionHolder)
        {
            if (configurations != null) ConfigurationHolder!.AppendConfigurations(configurations);
        }
    }
}

public class DefinedModifierTemplate : DefinedAssignableTemplate
{
    public DefinedModifierTemplate(string localizedName, Virial.Color color, IEnumerable<IConfiguration>? configurations = null,bool withConfigurationHolder = true, Func<bool>? optionHolderPredicate = null) : base(localizedName, color, withConfigurationHolder ? ConfigurationTab.Modifiers : null, optionHolderPredicate)
    {
        if (withConfigurationHolder)
        {
            if (configurations != null) ConfigurationHolder!.AppendConfigurations(configurations);
        }
    }
}

public class DefinedAllocatableModifierTemplate : DefinedModifierTemplate, HasAssignmentRoutine, RoleFilter, HasRoleFilter, ICodeName
{
    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.ModifierFilter?.Test((this as DefinedModifier)!) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.ModifierFilter?.ToggleAndShare((this as DefinedModifier)!);
    RoleFilter HasRoleFilter.RoleFilter => this;

    
    private IOrderedSharableVariable<int>? CrewmateAssignment = null;
    private IOrderedSharableVariable<int>? ImpostorAssignment = null;
    private IOrderedSharableVariable<int>? NeutralAssignment = null;
    private IOrderedSharableVariable<int>? CrewmateRandomAssignment = null;
    private IOrderedSharableVariable<int>? ImpostorRandomAssignment = null;
    private IOrderedSharableVariable<int>? NeutralRandomAssignment = null;
    private IOrderedSharableVariable<int>? CrewmateChance = null;
    private IOrderedSharableVariable<int>? ImpostorChance = null;
    private IOrderedSharableVariable<int>? NeutralChance = null;
    internal bool CanAssignTo(RoleCategory category) => category switch { RoleCategory.ImpostorRole => ImpostorAssignment, RoleCategory.CrewmateRole => CrewmateAssignment, RoleCategory.NeutralRole => NeutralAssignment, _ => null } != null;

    public DefinedAllocatableModifierTemplate(string localizedName, string codeName, Virial.Color color, IEnumerable<IConfiguration>? configurations = null, bool allocateToCrewmate = true, bool allocateToImpostor = true, bool allocateToNeutral = true) : base(localizedName, color)
    {
        this.codeName = codeName;
        string internalName = (this as DefinedAssignable).InternalName;
        
        IConfiguration GenerateConfiguration(string category, IOrderedSharableVariable<int> assignment, IOrderedSharableVariable<int> randomAssignment, IOrderedSharableVariable<int> chance)
        {
            var gui = NebulaAPI.GUI;
            var assignmentText = gui.LocalizedTextComponent("options.role." + category + "Count");


            List<GUIWidget> GetWidgets()
            {
                List<GUIWidget> widgets = [
                gui.Text(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), assignmentText),
                gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                gui.HorizontalMargin(0.1f),
                gui.Text(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), gui.FunctionalTextComponent(()=> assignment.CurrentValue.ToString())),
                gui.SpinButton(GUIAlignment.Center, v => { assignment.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                gui.HorizontalMargin(0.5f),
                gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleShortest), "options.role.randomCount"),
                gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                gui.HorizontalMargin(0.1f),
                gui.Text(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), gui.FunctionalTextComponent(()=> randomAssignment.CurrentValue.ToString())),
                gui.SpinButton(GUIAlignment.Center, v => { randomAssignment.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                gui.HorizontalMargin(0.2f),
                gui.Text(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), gui.FunctionalTextComponent(()=> chance.CurrentValue.ToString() + "%")),
                gui.SpinButton(GUIAlignment.Center, v => { chance.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                ];

                return widgets;
            }

            string GetValueString()
            {
                string str = assignmentText.GetString() + ": " + assignment.Value;
                if(randomAssignment.CurrentValue > 0)
                {
                    str += $" + {randomAssignment.Value}({chance.Value}%)";
                }
                return str;
            }

            return NebulaAPI.Configurations.Configuration(GetValueString, () => gui.HorizontalHolder(Media.GUIAlignment.Center, GetWidgets()));
        }

        if (allocateToCrewmate)
        {
            CrewmateAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.crewmate", (0, 15), 0);
            CrewmateRandomAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.crewmate.random", (0, 15), 0);
            CrewmateChance = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.crewmate.chance", (10, 90, 10), 90);
            ConfigurationHolder?.AppendConfiguration(GenerateConfiguration("crewmate", CrewmateAssignment, CrewmateRandomAssignment, CrewmateChance));
        }
        if (allocateToImpostor)
        {
            ImpostorAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.impostor", (0, 5), 0);
            ImpostorRandomAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.impostor.random", (0, 5), 0);
            ImpostorChance = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.impostor.chance", (10, 90, 10), 90);
            ConfigurationHolder?.AppendConfiguration(GenerateConfiguration("impostor", ImpostorAssignment, ImpostorRandomAssignment, ImpostorChance));
        }
        if (allocateToNeutral)
        {
            NeutralAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.neutral", (0, 15), 0);
            NeutralRandomAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.neutral.random", (0, 15), 0);
            NeutralChance = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.neutral.chance", (10, 90, 10), 90);
            ConfigurationHolder?.AppendConfiguration(GenerateConfiguration("neutral", NeutralAssignment, NeutralRandomAssignment, NeutralChance));
        }

        ConfigurationHolder!.SetDisplayState(() =>
        {
            if (
            (allocateToCrewmate && CrewmateAssignment!.CurrentValue > 0) ||
            (allocateToImpostor && ImpostorAssignment!.CurrentValue > 0) ||
            (allocateToNeutral && NeutralAssignment!.CurrentValue > 0))
                return ConfigurationHolderState.Emphasized;
            if (
            (allocateToCrewmate && CrewmateRandomAssignment!.CurrentValue > 0) ||
            (allocateToImpostor && ImpostorRandomAssignment!.CurrentValue > 0) ||
            (allocateToNeutral && NeutralRandomAssignment!.CurrentValue > 0))
                return ConfigurationHolderState.Activated;
            return ConfigurationHolderState.Inactivated;
        });

        //割り当て設定を上に持ってくるためにここで追加する
        if (configurations != null)ConfigurationHolder?.AppendConfigurations(configurations);
    }

    void HasAssignmentRoutine.TryAssign(Virial.Assignable.IRoleTable roleTable)
    {
        void TryAssign(RoleCategory category, int num, int randomNum, int chance)
        {
            int reallyNum = num;
            float chanceF = chance / 100f;
            for (int i = 0; i < randomNum; i++) if (!(chanceF < 1f) || (float)System.Random.Shared.NextDouble() < chanceF) reallyNum++;

            var players = roleTable.GetPlayers(category).Where(tuple => tuple.role.CanLoad(this)).OrderBy(i => Guid.NewGuid()).ToArray();
            reallyNum = Mathf.Min(players.Length, reallyNum);

            for (int i = 0; i < reallyNum; i++) SetModifier(roleTable, players[i].playerId);
        }

        TryAssign(RoleCategory.CrewmateRole, CrewmateAssignment?.CurrentValue ?? 0, CrewmateRandomAssignment?.CurrentValue ?? 0, CrewmateChance?.CurrentValue ?? 0);
        TryAssign(RoleCategory.ImpostorRole, ImpostorAssignment?.CurrentValue ?? 0, ImpostorRandomAssignment?.CurrentValue ?? 0, ImpostorChance?.CurrentValue ?? 0);
        TryAssign(RoleCategory.NeutralRole, NeutralAssignment?.CurrentValue ?? 0, NeutralRandomAssignment?.CurrentValue ?? 0, NeutralChance?.CurrentValue ?? 0);
    }

    protected virtual void SetModifier(IRoleTable roleTable, byte playerId) => roleTable.SetModifier(playerId, (this as DefinedModifier)!);

    int HasAssignmentRoutine.AssignPriority => 10;

    private string codeName;
    string ICodeName.CodeName => codeName;
}

public class DefinedGhostRoleTemplate : DefinedSingleAssignableTemplate, RoleFilter, HasRoleFilter
{
    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.GhostRoleFilter?.Test((this as DefinedGhostRole)!) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.GhostRoleFilter?.ToggleAndShare((this as DefinedGhostRole)!);
    RoleFilter HasRoleFilter.RoleFilter => this;

    public DefinedGhostRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, IConfiguration[]? configurations = null) : base(localizedName, color, category, team, true, ConfigurationTab.GhostRoles)
    {
        if(configurations != null) ConfigurationHolder?.AppendConfigurations(configurations);
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

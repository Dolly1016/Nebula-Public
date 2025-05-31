using Il2CppSystem.Linq.Expressions.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Components;
using Virial.Configuration;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;
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
            ConfigurationHolder = NebulaAPI.Configurations.Holder(NebulaAPI.GUI.FunctionalTextComponent(()=>(this as DefinedAssignable).DisplayColoredName, localizedName), NebulaAPI.GUI.LocalizedTextComponent("options.role." + this.LocalizedName + ".detail"), [tab], GameModes.AllGameModes, optionHolderPredicate, optionHolderState);
        
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
        private Func<AllocationParameters.ExtraAssignmentInfo[]?>? teamAssignments, othersAssignments;
        AllocationParameters.ExtraAssignmentInfo[] AllocationParameters.TeamAssignment => teamAssignments?.Invoke() ?? [];
        AllocationParameters.ExtraAssignmentInfo[] AllocationParameters.OthersAssignment => othersAssignments?.Invoke() ?? [];
        public StandardAssignmentParameters(string id, bool isImpostor, bool jacalized = false, Func<AllocationParameters.ExtraAssignmentInfo[]?>? teamAssignments = null, Func<AllocationParameters.ExtraAssignmentInfo[]?>? othersAssignments = null)
        {
            this.teamAssignments = teamAssignments;
            this.othersAssignments = othersAssignments;

            roleCountOption = NebulaAPI.Configurations.Configuration(id + ".count", (0, isImpostor ? 6 : 24), 0, title: NebulaAPI.GUI.TextComponent(jacalized ? NebulaTeams.JackalTeam.Color : Color.White, jacalized ? "options.role.count.jackalized" : "options.role.count"), predicate: ()=> !jacalized || NebulaAPI.Configurations.CanJackalize);

            roleChanceEntry = NebulaAPI.Configurations.SharableVariable(id + ".chance", (10, 100, 10), 100);
            roleSecondaryChanceEntry = NebulaAPI.Configurations.SharableVariable(id + ".secondaryChance", (0, 100, 10), 0);

            roleChanceEditor = NebulaAPI.Configurations.Configuration(
                () =>
                {
                    string str = NebulaAPI.Language.Translate(jacalized ? "options.role.chance.jackalized" : "options.role.chance").Color(jacalized ? NebulaTeams.JackalTeam.UnityColor : UnityEngine.Color.white) + ": " + roleChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage");
                    if (roleCountOption.GetValue() > 1 && roleSecondaryChanceEntry.CurrentValue > 0) str += (" (" + roleSecondaryChanceEntry.CurrentValue + NebulaAPI.Language.Translate("options.percentage") + ")").Color(UnityEngine.Color.gray);
                    return str;
                },
                () => {
                    var gui = NebulaAPI.GUI;
                    if (roleCountOption.GetValue() <= 1)
                    {
                        return gui.HorizontalHolder(Media.GUIAlignment.Left,
                        gui.Text(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), gui.TextComponent(jacalized ? NebulaTeams.JackalTeam.Color : Color.White, jacalized ? "options.role.chance.jackalized" : "options.role.chance")),
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
                        gui.Text(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), gui.TextComponent(jacalized ? NebulaTeams.JackalTeam.Color : Color.White, jacalized ? "options.role.chance.jackalized" : "options.role.chance")),
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
                () => roleCountOption.GetValue() > 0　&& (!jacalized || NebulaAPI.Configurations.CanJackalize));
        }

        IEnumerable<IConfiguration> AllocationParameters.Configurations => [roleCountOption, roleChanceEditor];
        int AllocationParameters.RoleCountSum => roleCountOption.GetValue();

        int AllocationParameters.RoleCount100 => roleChanceEntry.Value != 100 ? 0 : (roleSecondaryChanceEntry?.Value == 0 ? roleCountOption : Math.Min(1, roleCountOption));
        int AllocationParameters.RoleCountRandom => (this as AllocationParameters).RoleCountSum - (this as AllocationParameters).RoleCount100;
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

    private AllocationParameters? myJackalAssignmentParameters = null;
    AllocationParameters? DefinedSingleAssignable.JackalAllocationParameters => myJackalAssignmentParameters;

    protected RoleCategory Category { get; private init; }
    RoleCategory DefinedCategorizedAssignable.Category => Category;

    protected RoleTeam Team { get; private init; }
    RoleTeam DefinedSingleAssignable.Team => Team;

    public DefinedSingleAssignableTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, bool withAssignmentOption = true, ConfigurationTab? tab = null, Func<bool>? optionHolderPredicate = null, Func<AllocationParameters.ExtraAssignmentInfo[]?>? teamAssignments = null, Func<AllocationParameters.ExtraAssignmentInfo[]?>? othersAssignments = null) : base(localizedName, color, tab, optionHolderPredicate)
    {
        this.Category = category;
        this.Team = team;

        if (withAssignmentOption)
        {
            myAssignmentParameters = new StandardAssignmentParameters("role." + (this as DefinedAssignable).InternalName, category == RoleCategory.ImpostorRole, teamAssignments: teamAssignments, othersAssignments: othersAssignments);
            ConfigurationHolder?.AppendConfigurations(myAssignmentParameters.Configurations);

            ConfigurationHolder?.SetDisplayState(() =>
            {
                bool spawnRandom = false;
                bool spawn100 = false;
                if (myJackalAssignmentParameters != null && NebulaAPI.Configurations.CanJackalize && myJackalAssignmentParameters.RoleCountSum > 0)
                {
                    spawnRandom = true;
                    if (myJackalAssignmentParameters.GetRoleChance(1) == 100) spawn100 = true;
                }
                if(myAssignmentParameters.RoleCountSum > 0)
                {
                    spawnRandom = true;
                    if (myAssignmentParameters.GetRoleChance(1) == 100) spawn100 = true;
                }

                if (spawn100) return ConfigurationHolderState.Emphasized;
                if (spawnRandom) return ConfigurationHolderState.Activated;
                return ConfigurationHolderState.Inactivated;
            });

            if((this as DefinedRole)?.IsJackalizable ?? false)
            {
                myJackalAssignmentParameters = new StandardAssignmentParameters("role." + (this as DefinedAssignable).InternalName + ".jackalized", false, true);
                ConfigurationHolder?.AppendConfigurations(myJackalAssignmentParameters.Configurations);
            }
        }
    }

    bool ISpawnable.IsSpawnable => (myAssignmentParameters?.RoleCountSum ?? 0) > 0;
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
        if (assignable is DefinedGhostRoleTemplate dgrt) return ((dgrt as DefinedCategorizedAssignable).Category & Category) != 0;
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

    public DefinedRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, IEnumerable<IConfiguration>? configurations = null, bool withAssignmentOption = true, bool withOptionHolder = true, Func<bool>? optionHolderPredicate = null, Func<AllocationParameters.ExtraAssignmentInfo[]?>? teamAssignments = null, Func<AllocationParameters.ExtraAssignmentInfo[]?>? othersAssignments = null) : base(localizedName, color, category, team, withAssignmentOption, withOptionHolder ? category : null, optionHolderPredicate, teamAssignments, othersAssignments)
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

public abstract class DefinedSingleAbilityRoleTemplate<Ability> : DefinedRoleTemplate, DefinedSingleAbilityRole<Ability> where Ability : class, IPlayerAbility
{
    public abstract Ability CreateAbility(Virial.Game.Player player, int[] arguments);
    public virtual string? GetDisplayAbilityName(Ability ability) => null;
    string DefinedRole.GetDisplayName(Virial.Game.IPlayerAbility ability) => (ability is Ability a ? GetDisplayAbilityName(a) : null) ?? (this as DefinedRole).DisplayName;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Virial.Game.Player player, int[] arguments) => new RuntimeSingleAbilityAssignable<Ability>(player, this, arguments);
    
    public DefinedSingleAbilityRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, IEnumerable<IConfiguration>? configurations = null, bool withAssignmentOption = true, bool withOptionHolder = true, Func<bool>? optionHolderPredicate = null) : base(localizedName, color, category, team, configurations, withAssignmentOption, withOptionHolder, optionHolderPredicate)
    {
    }
}

/// <summary>
/// 簒奪可能で、簒奪した能力が別の能力として記述される役職のテンプレートです。
/// </summary>
/// <typeparam name="Ability">本来の能力。簒奪された後は能力が壊れた方が望ましい。</typeparam>
/// <typeparam name="UsurpedAbility">簒奪して得られる能力。</typeparam>
public abstract class DefinedUsurpableAdvancedRoleTemplate<Ability, UsurpedAbility> : DefinedRoleTemplate, DefinedSingleAbilityRole<Ability> where Ability : class, IUsurpableAbility where UsurpedAbility : class, IUsurpableAbility
{
    public abstract Ability CreateAbility(Virial.Game.Player player, int[] arguments);
    public abstract UsurpedAbility CreateUsurpedAbility(Virial.Game.Player player, int[] arguments);
    public virtual string? GetDisplayAbilityName(Ability ability) => null;
    public virtual string? GetDisplayUsurpedAbilityName(UsurpedAbility ability) => null;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Virial.Game.Player player, int[] arguments) => new RuntimeSingleAbilityAssignable<Ability>(player, this, arguments);
    IUsurpableAbility? DefinedRole.GetUsurpedAbility(Virial.Game.Player player, int[] arguments) => CreateUsurpedAbility(player, arguments);
    string DefinedRole.GetDisplayName(IPlayerAbility ability)
    {
        if (ability is Ability a)
        {
            return GetDisplayAbilityName(a) ?? (this as DefinedRole).DisplayName;
        }else if (ability is UsurpedAbility u)
        {
            return GetDisplayUsurpedAbilityName(u) ?? (this as DefinedRole).DisplayName;
        }
        return (this as DefinedRole).DisplayName;
    }

    public DefinedUsurpableAdvancedRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, RoleTeam team, IEnumerable<IConfiguration>? configurations = null, bool withAssignmentOption = true, bool withOptionHolder = true, Func<bool>? optionHolderPredicate = null) : base(localizedName, color, category, team, configurations, withAssignmentOption, withOptionHolder, optionHolderPredicate)
    {
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

public class ByCategoryAllocatorOptions : IAssignToCategorizedRole
{
    public record CategoryOption(IOrderedSharableVariable<int> Assignment, IOrderedSharableVariable<int> RandomAssignment, IOrderedSharableVariable<int> Chance)
    {
        public int CalcedRandomAssignment { get
            {
                int num = 0;
                float chanceF = Chance.Value / 100f;
                for (int i = 0; i < RandomAssignment.Value; i++) if (!(chanceF < 1f) || (float)System.Random.Shared.NextDouble() < chanceF) num++;
                return num;
            } }

        private string categoryName;
        /*
                    CrewmateAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.crewmate", (0, 15), 0);
            CrewmateRandomAssignment = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.crewmate.random", (0, 15), 0);
            CrewmateChance = NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment.crewmate.chance", (10, 90, 10), 90);
         */
        public CategoryOption(int max, string categoryName, string internalName, IConfigurationHolder holder) : this(
            NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment." + categoryName, (0, max), 0),
            NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment." + categoryName + ".random", (0, max), 0),
            NebulaAPI.Configurations.SharableVariable("role." + internalName + ".assignment." + categoryName + ".chance", (10, 90, 10), 90)
            )
        {
            this.categoryName = categoryName;
            holder.AppendConfiguration(GenerateConfiguration());
        }

        private IConfiguration GenerateConfiguration()
        {
            var gui = NebulaAPI.GUI;
            var assignmentText = gui.LocalizedTextComponent("options.role." + categoryName + "Count");


            List<GUIWidget> GetWidgets()
            {
                List<GUIWidget> widgets = [
                gui.Text(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleHalf), assignmentText),
                gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                gui.HorizontalMargin(0.1f),
                gui.Text(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), gui.FunctionalTextComponent(()=> Assignment.CurrentValue.ToString())),
                gui.SpinButton(GUIAlignment.Center, v => { Assignment.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                gui.HorizontalMargin(0.5f),
                gui.LocalizedText(Media.GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsTitleShortest), "options.role.randomCount"),
                gui.RawText(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsFlexible), ":"),
                gui.HorizontalMargin(0.1f),
                gui.Text(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), gui.FunctionalTextComponent(()=> RandomAssignment.CurrentValue.ToString())),
                gui.SpinButton(GUIAlignment.Center, v => { RandomAssignment.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                gui.HorizontalMargin(0.2f),
                gui.Text(GUIAlignment.Center, gui.GetAttribute(Text.AttributeAsset.OptionsValueShorter), gui.FunctionalTextComponent(()=> Chance.CurrentValue.ToString() + "%")),
                gui.SpinButton(GUIAlignment.Center, v => { Chance.ChangeValue(v, true); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }),
                ];

                return widgets;
            }

            string GetValueString()
            {
                string str = assignmentText.GetString() + ": " + Assignment.Value;
                if (RandomAssignment.CurrentValue > 0)
                {
                    str += $" + {RandomAssignment.Value}({Chance.Value}%)";
                }
                return str;
            }

            return NebulaAPI.Configurations.Configuration(GetValueString, () => gui.HorizontalHolder(Media.GUIAlignment.Center, GetWidgets()));
        }
    }
    private IntegerConfiguration? maxCount = null;
    private CategoryOption? impostors = null, neutral = null, crewmates = null;
    public int MaxCount { get { int count = maxCount?.GetValue() ?? 0; return count == 0 ? -1 : count; } }


    /// <summary>
    /// 指定カテゴリーのオプションを返します。
    /// 標準の設定で指定カテゴリーで割り当てがなされない場合、nullが返ります。
    /// </summary>
    /// <param name="roleCategory"></param>
    /// <returns></returns>
    public CategoryOption? GetOptions(RoleCategory roleCategory) => roleCategory switch { RoleCategory.ImpostorRole => impostors, RoleCategory.CrewmateRole => crewmates, RoleCategory.NeutralRole => neutral, _ => null };
    internal bool CanAssignTo(RoleCategory category) => GetOptions(category) != null;

    void IAssignToCategorizedRole.GetAssignProperties(Virial.Assignable.RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        var options = GetOptions(category);
        assign100 = options?.Assignment.Value ?? 0;
        assignRandom = options?.RandomAssignment.Value ?? 0;
        assignChance = options?.Chance.Value ?? 0;
    }

    public bool CanAssignOnThisGameByConfiguration(RoleCategory category)
    {
        var options = GetOptions(category);
        if (options == null) return false;
        return options.Assignment.Value + options.RandomAssignment.Value > 0;
    }

    public ConfigurationHolderState GetDisplayState()
    {
        if (((crewmates?.Assignment.Value ?? 0) > 0) || ((impostors?.Assignment.Value ?? 0) > 0) || ((neutral?.Assignment.Value ?? 0) > 0))
            return ConfigurationHolderState.Emphasized;
        if (((crewmates?.RandomAssignment.Value ?? 0) > 0) || ((impostors?.RandomAssignment.Value ?? 0) > 0) || ((neutral?.RandomAssignment.Value ?? 0) > 0))
            return ConfigurationHolderState.Activated;
        return ConfigurationHolderState.Inactivated;
    }

    public ByCategoryAllocatorOptions(string internalName, IConfigurationHolder holder, bool canAssignToCrewmate, bool canAssignToImpostor, bool canAssignToNeutral)
    {
        if (canAssignToCrewmate) crewmates = new(24, "crewmate", internalName, holder);
        if (canAssignToImpostor) impostors = new(6, "impostor", internalName, holder);
        if (canAssignToNeutral) neutral = new(24, "neutral", internalName, holder);

        //2カテゴリ以上に割り当てられるなら、最大数を設定できる。
        if ((canAssignToCrewmate ? 1 : 0) + (canAssignToImpostor ? 1 : 0) + (canAssignToNeutral ? 1 : 0) >= 2)
        {
            maxCount = NebulaAPI.Configurations.Configuration("role." + internalName + ".assignment.max", (0, 15), 0,decorator: num => num == 0 ? NebulaAPI.Language.Translate("options.role.maxCount.unlimited") : num.ToString(), title: NebulaAPI.GUI.LocalizedTextComponent("options.role.maxCount"));
            holder.AppendConfiguration(maxCount);
        }
    }
}

public class DefinedAllocatableModifierTemplate : DefinedModifierTemplate, HasAssignmentRoutine, RoleFilter, HasRoleFilter, ICodeName, ISpawnable, IAssignToCategorizedRole
{
    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.ModifierFilter?.Test((this as DefinedModifier)!) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.ModifierFilter?.ToggleAndShare((this as DefinedModifier)!);
    void AssignableFilter<DefinedRole>.SetAndShare(DefinedRole role, bool val) => role.ModifierFilter?.SetAndShare((this as DefinedModifier)!, val);
    RoleFilter HasRoleFilter.RoleFilter => this;
    bool ISpawnable.IsSpawnable => ConfigurationHolder?.DisplayOption != ConfigurationHolderState.Inactivated;

    ByCategoryAllocatorOptions allocatorOptions;

    internal bool CanAssignTo(RoleCategory category) => allocatorOptions.CanAssignTo(category);

    /// <summary>
    /// 今のゲームで、オプションの都合に依って割り当てられるかどうか調べます。
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    public bool CanAssignOnThisGameByConfiguration(RoleCategory category) => allocatorOptions.CanAssignOnThisGameByConfiguration(category);
    public DefinedAllocatableModifierTemplate(string localizedName, string codeName, Virial.Color color, IEnumerable<IConfiguration>? configurations = null, bool allocateToCrewmate = true, bool allocateToImpostor = true, bool allocateToNeutral = true) : base(localizedName, color)
    {
        this.codeName = codeName;
        string internalName = (this as DefinedAssignable).InternalName;

        allocatorOptions = new(internalName, ConfigurationHolder!, allocateToCrewmate, allocateToImpostor, allocateToNeutral);

        ConfigurationHolder!.SetDisplayState(allocatorOptions.GetDisplayState);

        //割り当て設定を上に持ってくるためにここで追加する
        if (configurations != null)ConfigurationHolder?.AppendConfigurations(configurations);
    }

    void HasAssignmentRoutine.TryAssign(Virial.Assignable.IRoleTable roleTable)
    {
        // 0を下回らないように、最大値の中に納まるよう値を減少させる。戻り値は余剰。
        int LessenRandomly(int[] num, int max)
        {
            //最大値が0を下回るものは無視する。
            if (max < 0) return max;

            int left = num.Sum() - max;
            if (left <= 0) return -left;

            List<int> moreThanZeroIndex = new();
            for (int i = 0; i < num.Length; i++) if (num[i] > 0) moreThanZeroIndex.Add(i);

            while (true)
            {
                if (left <= 0) return 0;

                int targetIndex = moreThanZeroIndex[System.Random.Shared.Next(moreThanZeroIndex.Count)];
                num[targetIndex]--;
                if (num[targetIndex] == 0) moreThanZeroIndex.Remove(targetIndex);

                left--;
            }
        }
        RoleCategory[] categories = [RoleCategory.CrewmateRole, RoleCategory.ImpostorRole, RoleCategory.NeutralRole];
        var players = categories.Select(c => roleTable.GetPlayers(c).Where(tuple => tuple.role.CanLoad(this)).OrderBy(_ => Guid.NewGuid()).ToArray()).ToArray();
        int[] assignables = players.Select(p => p.Count()).ToArray();
        int[] num = categories.Select(c => allocatorOptions.GetOptions(c)?.Assignment.Value ?? 0).ToArray();
        for (int i = 0; i < categories.Length; i++) if (num[i] > assignables[i]) num[i] = assignables[i];
        int randomMax = LessenRandomly(num, allocatorOptions.MaxCount);

        if (randomMax != 0)
        {
            int[] randomNum = categories.Select(c => allocatorOptions.GetOptions(c)?.CalcedRandomAssignment ?? 0).ToArray();
            for (int i = 0; i < categories.Length; i++) if (num[i] + randomNum[i] > assignables[i]) randomNum[i] = assignables[i] - num[i];

            LessenRandomly(randomNum, randomMax);
            for(int i = 0;i< num.Length;i++) num[i] += randomNum[i];
        }

        for (int i = 0; i < categories.Length; i++) for (int p = 0; p < num[i]; p++) SetModifier(roleTable, players[i][p].playerId);
    }

    protected virtual void SetModifier(IRoleTable roleTable, byte playerId) => roleTable.SetModifier(playerId, (this as DefinedModifier)!);

    public void GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        ((IAssignToCategorizedRole)allocatorOptions).GetAssignProperties(category, out assign100, out assignRandom, out assignChance);
    }

    int HasAssignmentRoutine.AssignPriority => 10;

    private string codeName;
    string ICodeName.CodeName => codeName;
}

public class DefinedGhostRoleTemplate : DefinedAssignableTemplate, DefinedCategorizedAssignable, RoleFilter, HasRoleFilter, IHasCategorizedRoleAllocator<DefinedGhostRole>, IAssignToCategorizedRole
{
    public class GhostAllocator : ICategorizedRoleAllocator<DefinedGhostRole>
    {
        public GhostAllocator(DefinedGhostRoleTemplate role) { 
            ghostRole = role;
            left = ghostRole.allocatorOptions.MaxCount;
            if (left <= 0) left = 99;

            var crew = ghostRole.allocatorOptions.GetOptions(RoleCategory.CrewmateRole);
            var imp = ghostRole.allocatorOptions.GetOptions(RoleCategory.ImpostorRole);
            var neu = ghostRole.allocatorOptions.GetOptions(RoleCategory.NeutralRole);
            categoryLeft = [
                (crew?.Assignment.Value ?? 0, crew?.RandomAssignment.Value ?? 0),
                (imp?.Assignment.Value ?? 0, imp?.RandomAssignment.Value ?? 0),
                (neu?.Assignment.Value ?? 0, neu?.RandomAssignment.Value ?? 0)];
        }
        DefinedGhostRoleTemplate ghostRole;
        int left;
        (int left, int randomLeft)[] categoryLeft;
        DefinedGhostRole ICategorizedRoleAllocator<DefinedGhostRole>.MyRole => (ghostRole as DefinedGhostRole)!;

        int CategoryToIndex(RoleCategory category) => category switch { RoleCategory.CrewmateRole => 0, RoleCategory.ImpostorRole => 1, RoleCategory.NeutralRole => 2 };
        

        void ICategorizedRoleAllocator<DefinedGhostRole>.ConsumeCount(RoleCategory category)
        {
            left--;
            if (categoryLeft[CategoryToIndex(category)].left > 0) 
                categoryLeft[CategoryToIndex(category)].left--;
            else 
                categoryLeft[CategoryToIndex(category)].randomLeft--;
        }

        int ICategorizedRoleAllocator<DefinedGhostRole>.GetChance(RoleCategory category)
        {
            if (left <= 0) return 0;
            var cLeft = categoryLeft[CategoryToIndex(category)];
            if (cLeft.left > 0) return 100;
            if (cLeft.randomLeft > 0) return ghostRole.allocatorOptions.GetOptions(category)?.Chance.Value ?? 0;
            return 0;
        }
    }
    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.GhostRoleFilter?.Test((this as DefinedGhostRole)!) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.GhostRoleFilter?.ToggleAndShare((this as DefinedGhostRole)!);
    void AssignableFilter<DefinedRole>.SetAndShare(DefinedRole role, bool val) => role.GhostRoleFilter?.SetAndShare((this as DefinedGhostRole)!, val);

    ICategorizedRoleAllocator<DefinedGhostRole> IHasCategorizedRoleAllocator<DefinedGhostRole>.GenerateRoleAllocator() => new GhostAllocator(this);

    public void GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        ((IAssignToCategorizedRole)allocatorOptions).GetAssignProperties(category, out assign100, out assignRandom, out assignChance);
    }

    RoleFilter HasRoleFilter.RoleFilter => this;
    protected RoleCategory Category { get; private init; }
    RoleCategory DefinedCategorizedAssignable.Category => Category;
    ByCategoryAllocatorOptions allocatorOptions;
    public DefinedGhostRoleTemplate(string localizedName, Virial.Color color, RoleCategory category, IConfiguration[]? configurations = null) : base(localizedName, color, ConfigurationTab.GhostRoles)
    {
        Category = category;

        allocatorOptions = new(localizedName, ConfigurationHolder!, (category & RoleCategory.CrewmateRole) != 0, (category & RoleCategory.ImpostorRole) != 0, (category & RoleCategory.NeutralRole) != 0);

        ConfigurationHolder!.SetDisplayState(allocatorOptions.GetDisplayState);

        //割り当て設定を上に持ってくるためにここで追加する
        if (configurations != null) ConfigurationHolder?.AppendConfigurations(configurations);
    }
}

/// <summary>
/// 実行時役職のテンプレートです。
/// </summary>
public class RuntimeAssignableTemplate : SimpleLifespan, IBindPlayer
{
    private Virial.Game.Player myPlayer { get; init; }
    protected bool AmOwner => myPlayer.AmOwner;
    public Virial.Game.Player MyPlayer => myPlayer;

    public RuntimeAssignableTemplate(Player myPlayer)
    {
        this.myPlayer = myPlayer;
    }
}

public abstract class RuntimeVentRoleTemplate : RuntimeAssignableTemplate, RuntimeRole
{
    public RuntimeVentRoleTemplate(Player myPlayer, IVentConfiguration ventConfiguration) : base(myPlayer)
    {
        this.canUseVent = ventConfiguration.CanUseVent;
        float cooldown = ventConfiguration.CoolDown;
        if (cooldown > 0f)
        {
            this.ventCoolDown = NebulaAPI.Modules.Timer(this, cooldown).SetAsAbilityTimer().ResetsAtTaskPhase();
            this.ventCoolDown.Start();
        }
        float duration = ventConfiguration.Duration;
        if (duration > 0f)
        {
            this.ventDuration = NebulaAPI.Modules.Timer(this, duration);
        }
    }

    private GameTimer? ventCoolDown;
    private GameTimer? ventDuration;
    private bool canUseVent;

    GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
    GameTimer? RuntimeRole.VentDuration => ventDuration;
    bool RuntimeRole.CanUseVent => canUseVent;

    public abstract DefinedRole Role { get; }
    public abstract void OnActivated();
}

public class RuntimeSingleAbilityAssignable<Ability> : RuntimeAssignableTemplate, RuntimeRole where Ability : class, IPlayerAbility
{
    public Ability MyAbility { get;protected set; }

    DefinedRole RuntimeRole.Role => role;
    private DefinedSingleAbilityRole<Ability> role;
    private int[] cachedArguments;
    public RuntimeSingleAbilityAssignable(Player myPlayer, DefinedSingleAbilityRole<Ability> role, int[] arguments) : base(myPlayer)
    {
        this.role = role;
        this.cachedArguments = arguments;
    }

    public virtual void OnActivated()
    {
        MyAbility = role.CreateAbility(MyPlayer, cachedArguments).Register(this);
    }
    void RuntimeRole.Usurp() => (MyAbility as IUsurpableAbility)?.Usurp();
    int[] RuntimeAssignable.RoleArguments => MyAbility.AbilityArguments;
    int[] RuntimeRole.UsurpedAbilityArguments => MyAbility.AbilityArguments;
    IEnumerable<IPlayerAbility> GetAbilities()
    {
        yield return MyAbility;
        foreach (var sa in MyAbility.SubAbilities) yield return sa;
    }
    IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => GetAbilities();

    string RuntimeAssignable.DisplayName => role.GetDisplayAbilityName(MyAbility) ?? role.DisplayName;
    string RuntimeAssignable.DisplayColoredName => (this as RuntimeAssignable).DisplayName.Color(role.UnityColor);
}

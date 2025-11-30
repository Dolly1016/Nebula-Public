using Il2CppSystem.Runtime.CompilerServices;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Media;
using Virial.Text;

namespace Nebula.Configuration;

public abstract class AssignableFilterConfigurationValue<T> where T : class, Virial.Assignable.ICodeName, Virial.Assignable.IRoleID
{
    private const int UnitSize = 30;
    internal class FilterSharableVariable : ISharableVariable<int>
    {
        private string name;
        private int id;
        private int currentValue;
        private AssignableFilterConfigurationValue<T> myFilter;
        private int index;

        public FilterSharableVariable(AssignableFilterConfigurationValue<T> filter, int index)
        {
            this.name = filter.Id + index;
            this.id = -1;
            this.index = index;
            this.myFilter = filter;

            currentValue = filter.ToSharableValueFromLocal(this.index);

            ConfigurationValues.RegisterEntry(this);
        }

        string ISharableEntry.Name => name;

        int ISharableEntry.Id { get => id; set => id = value; }
        int ISharableEntry.RpcValue { get => currentValue; set => currentValue = value; }

        int ISharableVariable<int>.CurrentValue
        {
            get => currentValue; 
            set
            {
                ConfigurationValues.AssertOnChangeOptionValue();
                if (currentValue != value)
                {
                    currentValue = value;
                    ConfigurationValues.TryShareOption(this);
                }
            }
        }

        int Virial.Compat.Reference<int>.Value => currentValue;

        void ISharableVariable<int>.SetValueWithoutSaveUnsafe(int value)
        {
            currentValue = value;
        }
        void ISharableEntry.RestoreSavedValue()
        {
            currentValue = myFilter.ToSharableValueFromLocal(this.index);
        }
    }

    private StringArrayDataEntry dataEntry;
    abstract protected IEnumerable<T> AllAssignables { get; }
    private ISharableVariable<int>[] sharableVariables;
    
    private int ToSharableValueFromLocal(int index)
    {
        int value = 0;
        int idMin = index * UnitSize;
        int idMax = (index + 1) * UnitSize;
        foreach (var a in AllAssignables)
        {
            if(a.Id >= idMin && a.Id < idMax && dataEntry.Value.Contains(a.CodeName))
            {
                value |= 1 << a.Id % UnitSize;
            }
        }
        return value;
    }
    
    public string Id { get; private set; }
    public AssignableFilterConfigurationValue(string id)
    {
        Id = id;
        dataEntry = new StringArrayDataEntry(id, Configuration.ConfigurationValues.ConfigurationSaver, []);

        void GenerateSharable()
        {
            int length = (AllAssignables.Max(a => a.Id) + 1) / UnitSize + 1;

            sharableVariables = new ISharableVariable<int>[length];
            for (int i = 0; i < length; i++)
            {
                sharableVariables[i] = new FilterSharableVariable(this, i);
            }
        }

        NebulaAPI.Preprocessor?.SchedulePreprocess(PreprocessPhase.FixStructureRoleFilter, GenerateSharable);

    }

    /// <summary>
    /// 削除するために、除外リストに追加する。
    /// </summary>
    /// <param name="assignable"></param>
    public void RemoveWithLocal(T assignable, bool saveLocal = true)
    {
        sharableVariables[assignable.Id / UnitSize].CurrentValue |= (1 << assignable.Id % UnitSize);
        if (saveLocal) Save();
    }

    /// <summary>
    /// 追加するために、除外リストから外す。
    /// </summary>
    /// <param name="assignable"></param>
    public void AddWithLocal(T assignable, bool saveLocal = true)
    {
        sharableVariables[assignable.Id / UnitSize].CurrentValue &= ~(1 << assignable.Id % UnitSize);
        if (saveLocal) Save();
    }

    public void Save()
    {
        //除外リストを保存している点に注意
        dataEntry.Value = AllAssignables.Where(a => !Test(a)).Select(a => a.CodeName).ToArray();
    }

    public void ToggleAndShare(T assignable) => SetAndShare(assignable, !Test(assignable));

    public void SetAndShare(T assignable, bool val)
    {
        if (val)
            AddWithLocal(assignable);
        else
            RemoveWithLocal(assignable);
    }


    public bool Test(T? assignable)
    {
        if (assignable != null) return (sharableVariables[assignable.Id / UnitSize].CurrentValue & ((1 << assignable.Id % UnitSize))) == 0;
        return false;
    }
}

public class ModifierFilterImpl : AssignableFilterConfigurationValue<DefinedAllocatableModifier>, Virial.Configuration.ModifierFilter
{
    public ModifierFilterImpl(string id) : base(id)
    {
    }

    protected override IEnumerable<DefinedAllocatableModifier> AllAssignables => Roles.Roles.AllModifiers.Select(a => a as DefinedAllocatableModifier).Where(a => a is not null)!;

    bool Virial.Configuration.AssignableFilter<DefinedModifier>.Test(DefinedModifier assignable) => assignable is DefinedAllocatableModifier adm ? Test(adm) : false;
    void Virial.Configuration.AssignableFilter<DefinedModifier>.SetAndShare(Virial.Assignable.DefinedModifier assignable, bool val) { if (assignable is DefinedAllocatableModifier adm) SetAndShare(adm, val); }
    void Virial.Configuration.AssignableFilter<DefinedModifier>.ToggleAndShare(Virial.Assignable.DefinedModifier assignable) { if (assignable is DefinedAllocatableModifier adm) ToggleAndShare(adm); }
}

public class GhostRoleFilterImpl : AssignableFilterConfigurationValue<DefinedGhostRole>, Virial.Configuration.GhostRoleFilter
{
    public GhostRoleFilterImpl(string id) : base(id)
    {
    }

    protected override IEnumerable<DefinedGhostRole> AllAssignables => Roles.Roles.AllGhostRoles;

    bool Virial.Configuration.AssignableFilter<DefinedGhostRole>.Test(DefinedGhostRole assignable) => Test(assignable);
    void Virial.Configuration.AssignableFilter<DefinedGhostRole>.ToggleAndShare(Virial.Assignable.DefinedGhostRole assignable) => ToggleAndShare(assignable);
    void Virial.Configuration.AssignableFilter<DefinedGhostRole>.SetAndShare(Virial.Assignable.DefinedGhostRole assignable, bool val) => SetAndShare(assignable, val);
}


public static class RoleFilterHelper
{
    /// <summary>
    /// フィルターの内容を表示用の文字列で表現します。
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    public static string GetFilterDisplayString(DefinedAssignable roleFilter, bool canAssignToCrewmate, bool canAssignToImpostor, bool canAssignToNeutral)
    {
        List<DefinedRole> assignableCrewmate = new();
        List<DefinedRole> nonAssignableCrewmate = new();
        List<DefinedRole> assignableImpostor = new();
        List<DefinedRole> nonAssignableImpostor = new();
        List<DefinedRole> assignableNeutral = new();
        List<DefinedRole> nonAssignableNeutral = new();
        foreach (var r in Roles.Roles.AllRoles)
        {
            //ヘルプ画面に出現しない役職はスルー
            if (!r.ShowOnHelpScreen) continue;
            if (!r.IsSpawnable) continue;

            bool assignable = true;

            switch (r.Category)
            {
                case RoleCategory.CrewmateRole:
                    assignable &= canAssignToCrewmate;
                    assignable &= r.CanLoad(roleFilter);
                    (assignable ? assignableCrewmate : nonAssignableCrewmate).Add(r);
                    break;
                case RoleCategory.ImpostorRole:
                    assignable &= canAssignToImpostor;
                    assignable &= r.CanLoad(roleFilter);
                    (assignable ? assignableImpostor : nonAssignableImpostor).Add(r);
                    break;
                case RoleCategory.NeutralRole:
                    assignable &= canAssignToNeutral;
                    assignable &= r.CanLoad(roleFilter);
                    (assignable ? assignableNeutral : nonAssignableNeutral).Add(r);
                    break;
            }
        }

        int allAssignableSum = assignableCrewmate.Count + assignableImpostor.Count + assignableNeutral.Count;
        int allNonAssignableSum = nonAssignableCrewmate.Count + nonAssignableImpostor.Count + nonAssignableNeutral.Count;

        if (allAssignableSum == 0)
        {
            //出現なし
            return Language.Translate("roleFilter.none");
        }
        if (allNonAssignableSum == 0)
        {
            //割り当て不可能なし
            return Language.Translate("roleFilter.allPattern.all");
        }

        bool CheckCategoryAssignable(List<DefinedRole> assignables, List<DefinedRole> nonAssignables) => assignables.Count == allAssignableSum && nonAssignables.Count == 0;
        if (CheckCategoryAssignable(assignableImpostor, nonAssignableImpostor)) return Language.Translate("roleFilter.allPattern.impostor");
        if (CheckCategoryAssignable(assignableCrewmate, nonAssignableCrewmate)) return Language.Translate("roleFilter.allPattern.crewmate");
        if (CheckCategoryAssignable(assignableNeutral, nonAssignableNeutral)) return Language.Translate("roleFilter.allPattern.neutral");

        bool isMonoCategory = ((IEnumerable<IReadOnlyList<DefinedRole>>)[assignableCrewmate, assignableImpostor, assignableNeutral]).Count(list => list.Count > 0) == 1;
        if (allAssignableSum <= 6 && !isMonoCategory) {
            //割当先が比較的少ない場合
            return string.Join(Language.Translate("roleFilter.separator"), assignableImpostor.Concat(assignableCrewmate).Concat(assignableNeutral).Select(r => r.DisplayColoredName));
        }
        if (allNonAssignableSum <= 6 && !isMonoCategory)
        {
            //割当不可能な対象が比較的少ない場合
            string roles = string.Join(Language.Translate("roleFilter.separator"), nonAssignableImpostor.Concat(nonAssignableCrewmate).Concat(nonAssignableNeutral).Select(r => r.DisplayColoredName));
            return Language.Translate("roleFilter.exceptPattern.all").Replace("%ROLES%", roles);
        }

        //陣営ごとに文字列を作成して結合

        bool useShortName = Mathn.Min(assignableCrewmate.Count, nonAssignableCrewmate.Count) + Mathn.Min(assignableImpostor.Count, nonAssignableImpostor.Count) + Mathn.Min(assignableNeutral.Count, nonAssignableNeutral.Count) > 7;
        string separator = Language.Translate("roleFilter.separator");

        string GetCategoryString(string category, List<DefinedRole> assignable, List<DefinedRole> nonAssignable)
        {
            if (assignable.Count == 0) return "";
            if (nonAssignable.Count == 0) return Language.Translate("roleFilter.allPattern." + category);
            if (assignable.Count <= nonAssignable.Count)
                return string.Join(separator, assignable.Select(r => r.DisplayColoredName));
            else
                return Language.Translate("roleFilter.exceptPattern." + category).Replace("%ROLES%", string.Join(separator, nonAssignable.Select(r => useShortName ? r.DisplayColoredShort : r.DisplayColoredName)));
        }

        string impostorStr = canAssignToImpostor ? GetCategoryString("impostor", assignableImpostor, nonAssignableImpostor) : "";
        string crewmateStr = canAssignToCrewmate ? GetCategoryString("crewmate", assignableCrewmate, nonAssignableCrewmate) : "";
        string neutralStr = canAssignToNeutral ? GetCategoryString("neutral", assignableNeutral, nonAssignableNeutral) : "";

        return string.Join(Language.Translate("roleFilter.categorySeparator"), ((IEnumerable<string>)[impostorStr, crewmateStr, neutralStr]).Where(s => s.Length > 0));
    }
}

public class SimpleRoleFilterConfiguration : IConfiguration, IExclusiveAssignmentRule
{
    private const int UnitSize = 30;
    internal class FilterSharableVariable : ISharableVariable<int>
    {
        private string name;
        private int id;
        private int currentValue;
        private SimpleRoleFilterConfiguration myConfig;
        private int index;

        public FilterSharableVariable(SimpleRoleFilterConfiguration config, int index)
        {
            this.name = config.dataEntry.Name + index;
            this.id = -1;
            this.index = index;
            this.myConfig = config;

            currentValue = config.ToSharableValueFromLocal(this.index);

            ConfigurationValues.RegisterEntry(this);
        }

        string ISharableEntry.Name => name;

        int ISharableEntry.Id { get => id; set => id = value; }
        int ISharableEntry.RpcValue { get => currentValue; set => currentValue = value; }

        int ISharableVariable<int>.CurrentValue
        {
            get => currentValue;
            set
            {
                ConfigurationValues.AssertOnChangeOptionValue();
                if (currentValue != value)
                {
                    currentValue = value;
                    ConfigurationValues.TryShareOption(this);
                }
            }
        }

        int Virial.Compat.Reference<int>.Value => currentValue;

        void ISharableVariable<int>.SetValueWithoutSaveUnsafe(int value) => currentValue = value;

        void ISharableEntry.RestoreSavedValue() => currentValue = myConfig.ToSharableValueFromLocal(this.index);

    }


    StringArrayDataEntry dataEntry;
    ISharableVariable<int>[] sharableVariables;

    public SimpleRoleFilterConfiguration(string id)
    {
        dataEntry = new(id, ConfigurationValues.ConfigurationSaver, []);

        void GenerateSharable()
        {
            sharableVariables = new ISharableVariable<int>[Roles.Roles.AllRoles.Count / UnitSize + 1];

            int length = Roles.Roles.AllRoles.Count / UnitSize + 1;

            sharableVariables = new ISharableVariable<int>[length];
            for (int i = 0; i < length; i++)
            {
                sharableVariables[i] = new FilterSharableVariable(this, i);
            }
        }

        NebulaAPI.Preprocessor?.SchedulePreprocess(Virial.Attributes.PreprocessPhase.FixStructureRoleFilter, GenerateSharable);
    }

    bool IConfiguration.IsShown => true;


    /// <summary>
    /// 同じくこのフィルタに含まれている他の役職を列挙します。
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    public IEnumerable<DefinedRole> OnAssigned(DefinedRole role)
    {
        if (Contains(role)) foreach (var r in Roles.Roles.AllRoles.Where(r => r != role && Contains(r))) yield return r;
    }

    public void SaveLocal()
    {
        var cache = Roles.Roles.AllRoles.Where(InvertOption ? r => !Contains(r) && (RolePredicate?.Invoke(r) ?? true) : r => Contains(r)).ToArray();
        var array = cache.Select(r => r.InternalName).ToArray();
        dataEntry.Value = array;
    }

    public string ScrollerTag { get; init; } = "exclusiveRole";
    public bool InvertOption { get; init; } = false;
    public bool PreviewOnlySpawnableRoles { get; init; } = false;

    GUIWidgetSupplier IConfiguration.GetEditor()
    {
        return () => new HorizontalWidgetsHolder(GUIAlignment.Left,
        new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitleHalf), new TranslateTextComponent(this.dataEntry.Name)) { OverlayWidget = ConfigurationAssets.GetOptionOverlay(this.dataEntry.Name), OnClickText = ConfigurationAssets.GetCopyAction(this.dataEntry.Name) },
        ConfigurationAssets.Semicolon,
        GUI.API.HorizontalMargin(0.08f),
        new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), new LazyTextComponent(() => ValueAsDisplayString ?? "None")),
        new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButton), new TranslateTextComponent("options.exclusiveAssignment.edit")) { OnClick = _ => RoleOptionHelper.OpenFilterScreen(ScrollerTag, Roles.Roles.AllRoles.Where(r => RolePredicate?.Invoke(r) ?? true), r => Contains(r), null, r => { ToggleAndShare(r); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }) }
        );
    }

    public Predicate<DefinedRole>? RolePredicate { get; init; } = null;

    private string? GetValuesString(int newLinesPer = 0, string newLineStr = "\n", int max = -1)
    {
        var roleCount = Roles.Roles.AllRoles.Count;
        int[] flags = new int[roleCount];
        int containedNum = 0, notContainedNum = 0;
        for(int i = 0; i < roleCount; i++)
        {
            var role = Roles.Roles.AllRoles[i];
            if (PreviewOnlySpawnableRoles && !role.IsSpawnable) continue;
            if(RolePredicate?.Invoke(role) ?? true)
            {
                var contains = Contains(role);
                flags[i] = contains ? 2 : 1;
                if (contains)
                    containedNum++;
                else
                    notContainedNum++;
            }
        }

        if (containedNum == 0) return Language.Translate("roleFilter.none");
        if (notContainedNum == 0) return Language.Translate("roleFilter.shortcut.all");

        bool showContainedRoles = containedNum <= notContainedNum;
        int checkNum = showContainedRoles ? 2 : 1;
        StringBuilder builder = new();

        int n = 0;
        for (int i = 0; i < roleCount; i++)
        {
            if (flags[i] != checkNum) continue;

            if (max > 0 && n >= max)
            {
                return Language.Translate("roleFilter.preview.etc").Replace("%ROLES%", builder.ToString());
            }
            if (n > 0)
            {
                builder.Append(", ");
                if (newLinesPer > 0 && n % newLinesPer == 0)
                    builder.Append(newLineStr);
            }
            builder.Append(Roles.Roles.AllRoles[i].DisplayColoredName);
            n++;
        }
        if (!showContainedRoles) return Language.Translate("roleFilter.preview.excludes").Replace("%ROLES%", builder.ToString());
        return builder.ToString();
    }

    string? ValueAsDisplayString => GetValuesString(-1, "\n", 4);

    string? IConfiguration.GetDisplayText()
    {
        var str = GetValuesString(4, "\n    ");
        if (str == null) return null;

        return Language.Translate(this.dataEntry.Name) + ": " + str;
    }

    public bool Contains(DefinedRole role)
    {
        var result = (sharableVariables[role.Id / UnitSize].CurrentValue & (1 << (role.Id % UnitSize))) != 0;
        if (InvertOption) result = !result;
        result &= RolePredicate?.Invoke(role) ?? true;
        return result;
    }
    internal void ToggleAndShare(DefinedRole role)
    {
        sharableVariables[role.Id / UnitSize].CurrentValue ^= (1 << (role.Id % UnitSize));
        SaveLocal();
    }

    internal void SetAndShare(DefinedRole role, bool on)
    {
        if (InvertOption) on = !on;
        if (on)
            sharableVariables[role.Id / UnitSize].CurrentValue |= (1 << (role.Id % UnitSize));
        else
            sharableVariables[role.Id / UnitSize].CurrentValue &= ~(1 << (role.Id % UnitSize));
        SaveLocal();
    }

    private int ToSharableValueFromLocal(int index)
    {
        int value = 0;
        foreach (var role in Roles.Roles.GetRoles(UnitSize * index, UnitSize * (index + 1)))
        {
            if (dataEntry.Value.Contains(role.InternalName)) value |= 1 << (role.Id % UnitSize);
        }

        return value;
    }

    IEnumerable<DefinedRole> IExclusiveAssignmentRule.GetExclusiveRoles()
    {
        foreach(var role in Roles.Roles.AllRoles)
        {
            if (Contains(role))
            {
                yield return role;
            }
        }
    }
}
using Il2CppSystem.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Virial;
using Virial.Assignable;
using Virial.Configuration;

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
    private HashSet<T> myLocalCache;
    private int ToSharableValueFromLocal(int index) => myLocalCache.Aggregate(0, (val, a) => { if (a.Id >= index * UnitSize && a.Id < (index + 1) * UnitSize) return val | (1 << (a.Id % UnitSize)); else return val; });
    
    public string Id { get; private set; }
    public AssignableFilterConfigurationValue(string id)
    {
        Id = id;
        dataEntry = new StringArrayDataEntry(id, Configuration.ConfigurationValues.ConfigurationSaver, []);

        void RefreshCache()
        {
            myLocalCache = new(dataEntry.Value.Select(code => AllAssignables.FirstOrDefault(a => a.CodeName == code)).Where(a => a != null)!);
        }

        void GenerateSharable()
        {
            RefreshCache();

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
        myLocalCache.Add(assignable);

        if (saveLocal) Save();
    }

    /// <summary>
    /// 追加するために、除外リストから外す。
    /// </summary>
    /// <param name="assignable"></param>
    public void AddWithLocal(T assignable, bool saveLocal = true)
    {
        sharableVariables[assignable.Id / UnitSize].CurrentValue &= ~(1 << assignable.Id % UnitSize);
        myLocalCache.Remove(assignable);

        if (saveLocal) Save();
    }

    public void Save()
    {
        dataEntry.Value = myLocalCache.Select(a => a.CodeName).ToArray();
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
        foreach(var r in Roles.Roles.AllRoles)
        {
            //ヘルプ画面に出現しない役職はスルー
            if (!r.ShowOnHelpScreen) continue;

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
        int byCategorySum = Math.Min(assignableCrewmate.Count, nonAssignableCrewmate.Count) + Math.Min(assignableImpostor.Count, nonAssignableImpostor.Count) + Math.Min(assignableNeutral.Count, nonAssignableNeutral.Count);

        if(allAssignableSum == 0)
        {
            //出現なし
            return Language.Translate("roleFilter.none");
        }
        if(allNonAssignableSum == 0)
        {
            //割り当て不可能なし
            return Language.Translate("roleFilter.allPattern.all");
        }
        if(allAssignableSum <= 8) {
            //割当先が比較的少ない場合
            return string.Join(Language.Translate("roleFilter.separator"), assignableImpostor.Concat(assignableCrewmate).Concat(assignableNeutral).Select(r => r.DisplayColoredName));
        }
        if (allNonAssignableSum <= 8)
        {
            //割当不可能な対象が比較的少ない場合
            string roles = string.Join(Language.Translate("roleFilter.separator"), nonAssignableImpostor.Concat(nonAssignableCrewmate).Concat(nonAssignableNeutral).Select(r => r.DisplayColoredName));
            return Language.Translate("roleFilter.exceptPattern.all").Replace("%ROLES%", roles);
        }
        if(byCategorySum <= 6)
        {
            //陣営ごとに最適な表示をした方が良い場合
            string separator = Language.Translate("roleFilter.separator");
            string impostorStr, crewmateStr, neutralStr;

            string GetCategoryString(string category, List<DefinedRole> assignable, List<DefinedRole> nonAssignable)
            {
                if (assignable.Count == 0) return "";
                if (nonAssignable.Count == 0) return Language.Translate("roleFilter.allPattern." + category);
                if (assignable.Count <= nonAssignable.Count)
                    return string.Join(separator, assignable.Select(r => r.DisplayColoredName));
                else
                    return Language.Translate("roleFilter.exceptPattern." + category).Replace("%ROLES%", string.Join(separator, nonAssignable.Select(r => r.DisplayColoredName)));
            }

            impostorStr = GetCategoryString("impostor", assignableImpostor, nonAssignableImpostor);
            crewmateStr = GetCategoryString("crewmate", assignableCrewmate, nonAssignableCrewmate);
            neutralStr = GetCategoryString("neutral", assignableNeutral, nonAssignableNeutral);

            return string.Join(Language.Translate("roleFilter.categorySeparator"), ((string[])[impostorStr, crewmateStr, neutralStr]).Where(s => s.Length > 0));
        }

        //どうしようもない場合は略称で表示
        if(allAssignableSum <= allNonAssignableSum)
        {
            return string.Join(Language.Translate("roleFilter.separator"), assignableImpostor.Concat(assignableCrewmate).Concat(assignableNeutral).Select(r => r.DisplayColoredShort));
        }
        else
        {
            return string.Join(Language.Translate("roleFilter.separator"), assignableImpostor.Concat(assignableCrewmate).Concat(assignableNeutral).Select(r => r.DisplayColoredShort));
        }
    }
}
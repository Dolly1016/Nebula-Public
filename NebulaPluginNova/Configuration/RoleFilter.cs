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

    public void ToggleAndShare(T assignable)
    {
        bool contains = Test(assignable);
        if (contains)
            RemoveWithLocal(assignable);
        else
            AddWithLocal(assignable);
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
}
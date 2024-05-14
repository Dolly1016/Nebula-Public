using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            ConfigurationValues.AllEntries.Add(this);
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
    private HashSet<T> myLocalExcludedAssignableCache;
    private int ToSharableValueFromLocal(int index) => myLocalExcludedAssignableCache.Aggregate(0, (val, a) => { if (a.Id >= index * UnitSize && a.Id < (index + 1) * UnitSize) return val | (1 << (index % UnitSize)); else return val; });
    
    public string Id { get; private set; }
    public AssignableFilterConfigurationValue(string id)
    {
        Id = id;
        dataEntry = new StringArrayDataEntry(id, Configuration.ConfigurationValues.ConfigurationSaver, []);

        NebulaAPI.Preprocessor?.SchedulePreprocess(Virial.Runtime.PreprocessPhase.PostFixAssignables, () => {
            myLocalExcludedAssignableCache = new(dataEntry.Value.Select(code => AllAssignables.FirstOrDefault(a => a.CodeName == code)).Where(a => a != null)!);

            int length = AllAssignables.Max(a => a.Id) / UnitSize;

            sharableVariables = new ISharableVariable<int>[length];
            for (int i = 0;i< length; i++) {
                sharableVariables[i] = new FilterSharableVariable(this, i);
            }
        });

    }

    /// <summary>
    /// 削除するために、除外リストに追加する。
    /// </summary>
    /// <param name="assignable"></param>
    public void RemoveWithLocal(T assignable, bool saveLocal = true)
    {
        sharableVariables[assignable.Id / UnitSize].CurrentValue |= (1 << assignable.Id % UnitSize);
        myLocalExcludedAssignableCache.Add(assignable);

        if (saveLocal) Save();
    }

    /// <summary>
    /// 追加するために、除外リストから外す。
    /// </summary>
    /// <param name="assignable"></param>
    public void AddWithLocal(T assignable, bool saveLocal = true)
    {
        sharableVariables[assignable.Id / UnitSize].CurrentValue &= ~(1 << assignable.Id % UnitSize);
        myLocalExcludedAssignableCache.Remove(assignable);

        if (saveLocal) Save();
    }

    public void Save()
    {
        dataEntry.Value = myLocalExcludedAssignableCache.Select(a => a.CodeName).ToArray();
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

public class ModifierFilterImpl : AssignableFilterConfigurationValue<AllocatableDefinedModifier>, Virial.Configuration.ModifierFilter
{
    public ModifierFilterImpl(string id) : base(id)
    {
    }

    protected override IEnumerable<AllocatableDefinedModifier> AllAssignables => Roles.Roles.AllModifiers.Select(a => a as AllocatableDefinedModifier).Where(a => a is not null)!;

    bool Virial.Configuration.ModifierFilter.Test(DefinedModifier modifier) => modifier is AllocatableDefinedModifier adm ? Test(adm) : false;
}

public class GhostRoleFilterImpl : AssignableFilterConfigurationValue<DefinedGhostRole>, Virial.Configuration.GhostRoleFilter
{
    public GhostRoleFilterImpl(string id) : base(id)
    {
    }

    protected override IEnumerable<DefinedGhostRole> AllAssignables => Roles.Roles.AllGhostRoles;

    bool Virial.Configuration.GhostRoleFilter.Test(DefinedGhostRole ghostRole) => Test(ghostRole);
}
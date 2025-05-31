using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;

namespace Nebula.Configuration;
internal abstract class ConfigurationValueBase<T, LocalEntry> : ISharableVariable<T> where T : notnull where LocalEntry : DataEntry<T>
{
    protected LocalEntry localEntry { get; init; }
    protected T currentValue { get; set; }
    protected string name;
    protected string Name => name;
    public ConfigurationValueBase(string name, LocalEntry entry)
    {
        this.name = name;
        this.localEntry = entry;
        this.currentValue = entry.Value;

        ConfigurationValues.RegisterEntry(this);
    }

    string ISharableEntry.Name => localEntry.Name;
    
    int ISharableEntry.Id { get; set; }

    int ISharableEntry.RpcValue { get => RpcValue; set => RpcValue = value; }
    protected abstract int RpcValue { get; set; }

    T Virial.Compat.Reference<T>.Value => currentValue;

    T ISharableVariable<T>.CurrentValue
    {
        get => currentValue;
        set
        {
            ConfigurationValues.AssertOnChangeOptionValue();
            currentValue = value;
            localEntry.Value = value;
            ConfigurationValues.TryShareOption(this);
        }
    }

    void ISharableVariable<T>.SetValueWithoutSaveUnsafe(T value)
    {
        currentValue = value;
        localEntry.SetValueWithoutSave(value);
    }

    void ISharableEntry.RestoreSavedValue()
    {
        currentValue = localEntry.Value;
    }
}

internal abstract class ComparableConfigurationValueBase<T, LocalEntry> : ConfigurationValueBase<T, LocalEntry>, IOrderedSharableVariable<T> where T : struct where LocalEntry : DataEntry<T>
{
    private T[] myMapper;
    private int myIndex;

    /// <summary>
    /// 二つの値の差を求めます。
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    abstract protected T CalcAbsDiff(T t1, T t2);

    void IOrderedSharableEntry.ChangeValue(bool increase, bool allowLoop)
    {
        int nextIndex = myIndex + (increase ? 1 : -1);

        if (allowLoop)
        {
            //配列の長さの中に収める
            if (nextIndex < 0) nextIndex = myMapper.Length - 1;
            else nextIndex %= myMapper.Length;
        }
        else
        {
            nextIndex = Math.Clamp(nextIndex, 0, myMapper.Length);
        }

        myIndex = nextIndex;
        (this as ISharableVariable<T>).CurrentValue = myMapper[nextIndex];
    }

    private void AdjustValue()
    {
        if (myMapper.Length > 0)
        {
            T localVal = localEntry.Value;
            T nearVal = myMapper.MinBy(v => CalcAbsDiff(v, localVal));
            (this as ISharableVariable<T>).SetValueWithoutSaveUnsafe(nearVal);
            myIndex = Array.IndexOf(myMapper, nearVal);
        }
        else
        {
            LogUtils.WriteToConsole("Mapper's length is 0! (id:"+ name + ")");
            myIndex = Array.IndexOf(myMapper, 0);
        }
    }

    public ComparableConfigurationValueBase(string name, T[] mapper, LocalEntry entry) : base(name, entry)
    {
        this.myMapper = mapper;

        T localVal = entry.Value;
        int index = Array.IndexOf(myMapper, localVal);

        if (index == -1)
            AdjustValue();
        else
            myIndex = index;
    }

    protected override int RpcValue
    { 
        get { 
            if(!currentValue.Equals(myMapper[myIndex])) AdjustValue();
            return myIndex; }
        set { currentValue = myMapper[value]; myIndex = value; } 
    }
}

/// <summary>
/// 真偽値で保存されるオプション値
/// </summary>
internal class BoolConfigurationValue : ConfigurationValueBase<bool, BooleanDataEntry>, IOrderedSharableVariable<bool>
{
    public BoolConfigurationValue(string name, bool defaultValue) : base(name, new(name, ConfigurationValues.ConfigurationSaver, defaultValue)){}

    protected override int RpcValue { get => currentValue ? 1 : 0; set => currentValue = value == 1; }

    void IOrderedSharableEntry.ChangeValue(bool increase, bool allowLoop) => (this as ISharableVariable<bool>).CurrentValue = !currentValue;
}

/// <summary>
/// セレクションのインデックスで保存されるオプション値
/// </summary>
internal class SelectionConfigurationValue : ConfigurationValueBase<int, IntegerDataEntry>, IOrderedSharableVariable<int>
{
    private int length;

    public SelectionConfigurationValue(string name, int defaultValue, int length) : base(name, new(name, ConfigurationValues.ConfigurationSaver, defaultValue)) { 
        this.length = length;
    }

    protected override int RpcValue { get => currentValue; set => currentValue = value; }

    void IOrderedSharableEntry.ChangeValue(bool increase, bool allowLoop)
    {
        if (allowLoop)
        {
            //負数の剰余を取るケースを避けるため、lengthを足しこむ
            (this as ISharableVariable<int>).CurrentValue = (currentValue + length + (increase ? 1 : -1)) % length;
        }
        else
        {
            (this as ISharableVariable<int>).CurrentValue = Math.Clamp((currentValue + (increase ? 1 : -1)), 0, length);
        }
    }
}

/// <summary>
/// 実数値で保存されるオプション値
/// </summary>
internal class FloatConfigurationValue : ComparableConfigurationValueBase<float, FloatDataEntry>
{

    public FloatConfigurationValue(string name, float[] mapper, float defaultValue) : base(name, mapper, new(name, ConfigurationValues.ConfigurationSaver, defaultValue)) { }

    protected override float CalcAbsDiff(float t1, float t2) => Math.Abs(t1 - t2);
}

/// <summary>
/// 値で保存されるオプション値
/// </summary>
internal class IntegerConfigurationValue : ComparableConfigurationValueBase<int, IntegerDataEntry>
{

    public IntegerConfigurationValue(string name, int[] mapper, int defaultValue) : base(name, mapper, new(name, ConfigurationValues.ConfigurationSaver, defaultValue)) { }

    protected override int CalcAbsDiff(int t1, int t2) => Math.Abs(t1 - t2);
}

/// <summary>
/// 生の値で保存されるオプション値
/// </summary>
internal class RawIntegerSharableVariable : ISharableVariable<int>
{
    private string name;
    private int id;
    private int currentValue;
    private IntegerDataEntry localEntry;

    public RawIntegerSharableVariable(string id, int defaultValue)
    {
        this.name = id;
        this.id = -1;

        localEntry = new(name, ConfigurationValues.ConfigurationSaver, defaultValue);
        currentValue = localEntry.Value;

        ConfigurationValues.RegisterEntry(this);
    }

    string ISharableEntry.Name => name;

    int ISharableEntry.Id { get => id; set => id = value; }
    int ISharableEntry.RpcValue { get => currentValue; set => currentValue = value; }

    int ISharableVariable<int>.CurrentValue
    { get => currentValue; set {
            ConfigurationValues.AssertOnChangeOptionValue();
            currentValue = value;
            localEntry.Value = value;
            ConfigurationValues.TryShareOption(this);
        } }

    int Virial.Compat.Reference<int>.Value => currentValue;

    void ISharableVariable<int>.SetValueWithoutSaveUnsafe(int value)
    {
        currentValue = value;
        localEntry.SetValueWithoutSave(value);
    }
    void ISharableEntry.RestoreSavedValue()
    {
        currentValue = localEntry.Value;
    }
}
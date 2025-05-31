using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Virial.VisualProgramming;

public delegate bool VPTypeLimitation(IVPTypeProperty prop);
public interface IVPTypeProperty
{
    bool CanCastToNumeric { get; }
    bool CanCastToInteger => CanCastToNumeric;
    bool CanCastToFloat => CanCastToNumeric;
    bool CanCastToBool => CanCastToNumeric;
    bool CanCastToString => CanCastToNumeric;
    bool CanCastTo<T>() where T : class => false;
}

file class NumberVPTypeProperty : IVPTypeProperty
{
    bool IVPTypeProperty.CanCastToNumeric => true;
    bool CanCastToBool => false;
}

file class BoolVPTypeProperty : IVPTypeProperty
{
    bool IVPTypeProperty.CanCastToNumeric => true;
    bool CanCastToInteger => false;
    bool CanCastToFloat => false;
}

file class StringVPTypeProperty : IVPTypeProperty
{
    bool IVPTypeProperty.CanCastToNumeric => true;
    bool CanCastToInteger => false;
    bool CanCastToFloat => false;
    bool CanCastToBool => false;
}

file class HiddenVPTypeProperty : IVPTypeProperty
{
    bool IVPTypeProperty.CanCastToNumeric => false;
}

public static class VPTypeProperties
{
    public static readonly IVPTypeProperty Number = new NumberVPTypeProperty();
    public static readonly IVPTypeProperty String = new StringVPTypeProperty();
    public static readonly IVPTypeProperty Hidden = new HiddenVPTypeProperty();
}

public static class VPLimitations
{
    public static readonly VPTypeLimitation Numeric = prop => prop.CanCastToNumeric;
}

public interface IVPNumeric
{
    int GetInt() => throw new InvalidOperationException();
    float GetFloat() => throw new InvalidOperationException();
    bool GetBool() => throw new InvalidOperationException();
    string GetString() => throw new InvalidOperationException();

    void SetInt(int value) => throw new InvalidOperationException();
    void SetFloat(float value) => throw new InvalidOperationException();
    void SetBool(bool value) => throw new InvalidOperationException();
    void SetString(string value) => throw new InvalidOperationException();
    bool GetGeneric<T>([MaybeNullWhen(false)] out T value) where T : class { value = null; return false; }
    bool SetGeneric<T>(T value) where T : class => false;

    IVPTypeProperty GetProperty() => VPTypeProperties.Number;
}

public class VPInt : IVPNumeric
{
    private int number;
    public bool GetBool() => number == 1;
    public float GetFloat() => number;

    public int GetInt() => number;
    public string GetString() => number.ToString();

    public void SetInt(int value) => number = value;
    public void SetFloat(float value) => number = (int)value;
    public void SetBool(bool value) => number = value ? 1 : 0;
    public void SetString(string value) => number = int.Parse(value);

    public VPInt(int number)
    {
        this.number = number;
    }
}

public class VPFloat : IVPNumeric
{
    private float number;
    public bool GetBool() => number == 1f;
    public float GetFloat() => number;

    public int GetInt() => (int)number;
    public string GetString() => number.ToString();

    public void SetInt(int value) => number = value;
    public void SetFloat(float value) => number = value;
    public void SetBool(bool value) => number = value ? 1f : 0f;
    public void SetString(string value) => number = float.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);


    public VPFloat(float number)
    {
        this.number = number;
    }
}

public class VPBool : IVPNumeric
{
    private bool value;
    public bool GetBool() => value;
    public float GetFloat() => value ? 1f : 0f;

    public int GetInt() => value ? 1 : 0;
    public string GetString() => value.ToString();


    public void SetInt(int value) => this.value = value == 1;
    public void SetFloat(float value) => this.value = value == 1f;
    public void SetBool(bool value) => this.value = value;
    public void SetString(string value) => this.value = bool.Parse(value);


    public VPBool(bool value)
    {
        this.value = value;
    }
}

public class VPString : IVPNumeric
{
    private string value;
    public bool GetBool() => bool.Parse(value);
    public float GetFloat() => float.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

    public int GetInt() => int.Parse(value);
    public string GetString() => value;


    public void SetInt(int value) => this.value = value.ToString();
    public void SetFloat(float value) => this.value = value.ToString(CultureInfo.InvariantCulture);
    public void SetBool(bool value) => this.value = value.ToString();
    public void SetString(string value) => this.value = value;


    public VPString(string value)
    {
        this.value = value;
    }
}
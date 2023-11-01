using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules;

public interface INebulaProperty
{
    public string GetString() => "INVALID";
    public float GetFloat() => -1f;
    public byte GetByte() => byte.MaxValue;
    public int GetInteger() => -1;
    public byte[] GetByteArray() => new byte[0];
    public int[] GetIntegerArray() => new int[0];
    public int[] GetFloatArray() => new int[0];
}

public class NebulaFunctionProperty : INebulaProperty
{
    Func<string>? myFunc;
    Func<float>? myFloatFunc;
    Func<int>? myIntegerFunc;
    Func<byte>? myByteFunc;

    public NebulaFunctionProperty(string id, Func<string> func, Func<float> floatFunc)
    {
        myFunc = func;
        myFloatFunc = floatFunc;

        PropertyManager.Register(id, this);
    }

    public string GetString() { 
        return myFunc?.Invoke() ?? "undefined";
    }

    public float GetFloat()
    {
        return myFloatFunc?.Invoke() ?? 0f;
    }

    public byte GetByte()
    {
        return myByteFunc?.Invoke() ?? 0;
    }

    public int GetInteger()
    {
        return myIntegerFunc?.Invoke() ?? 0;
    }
}

public class NebulaInstantProperty : INebulaProperty
{
    public string? StringProperty = null;
    public byte? ByteProperty = null;
    public int? IntegerProperty = null;
    public float? FloatProperty = null;
    public byte[]? ByteArrayProperty = null;
    public int[]? IntegerArrayProperty = null;
    public float[]? FloatArrayProperty = null;

    public string GetString() => StringProperty ?? "undefined";
    public byte GetByte() => ByteProperty ?? byte.MaxValue;
    public int GetInteger() => IntegerProperty ?? -1;
    public float GetFloat() => FloatProperty ?? 0f;
    public byte[] GetByteArray() => ByteArrayProperty ?? new byte[0];
    public int[] GetIntegerArray() => IntegerArrayProperty ?? new int[0];
    public float[] GetFloatArray() => FloatArrayProperty ?? new float[0];
}

public interface IRuntimePropertyHolder
{
    bool TryGetProperty(string id,out INebulaProperty? property);
}

static public class PropertyManager
{
    static Dictionary<string, INebulaProperty> allProperties = new();
    static public void Register(string id, INebulaProperty property)
    {
        allProperties[id] = property;
    }

    static public INebulaProperty? GetProperty(string id)
    {
        //関数プロパティ
        {
            if (allProperties.TryGetValue(id, out var property)) return property;
        }

        //ゲーム内プロパティ
        {
            if (NebulaGameManager.Instance?.TryGetProperty(id, out var property) ?? false) return property;
        }

        return null;
    }
}

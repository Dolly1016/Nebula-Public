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
    public INebulaProperty Bind(string argument) { return this; }
}

//グローバルな空間に存在するプロパティ
public class NebulaGlobalFunctionProperty : INebulaProperty
{
    Func<string>? myFunc;
    Func<float>? myFloatFunc;
    Func<int>? myIntegerFunc;
    Func<byte>? myByteFunc;
    Func<string, INebulaProperty>? myBindFunc;

    public NebulaGlobalFunctionProperty(string id, Func<string> func, Func<float> floatFunc, Func<string, INebulaProperty>? bindFunc = null)
    {
        myFunc = func;
        myFloatFunc = floatFunc;
        myBindFunc = bindFunc;

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

    public INebulaProperty Bind(string argument) { return myBindFunc?.Invoke(argument) ?? this; }
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
        var splitted = id.Split('?');
        if (splitted.Length == 1 || splitted.Length == 2)
        {
            INebulaProperty? prop = null;
            //関数プロパティ
            if (allProperties.TryGetValue(splitted[0], out var propInGlob)) prop = propInGlob;
            //ゲーム内プロパティ
            if (NebulaGameManager.Instance?.TryGetProperty(splitted[0], out var propInGame) ?? false) prop = propInGame;

            return splitted.Length == 2 ? prop?.Bind(splitted[1]) : prop;
        }

        return null;
    }
}

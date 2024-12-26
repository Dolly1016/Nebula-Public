using Innersloth.IO;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Nebula.Modules;

public interface IDataEntry
{
    string Serialize();
    void DeserializeAndSetWithoutSave(string val);
    bool ShouldWrite { get; }
    string Id { get; }
}

public abstract class DataEntry<T> : IDataEntry where T : notnull
{
    private T value;
    string name;
    DataSaver saver;
    public bool ShouldWrite { get; private init; }
    public T Value
    {
        get { return value; }
        set
        {
            this.value = value;
            saver.SetValue(name, Serialize(value));
        }
    }

    public string Name => name;

    public void SetValueWithoutSave(T value)
    {
        this.value = value;
        saver.SetValue(name, Serialize(value), true);
    }

    abstract protected bool Equals(T val1, T val2);

    public DataEntry(string name, DataSaver saver, T defaultValue, string? defaultSource = null, bool shouldWrite = true)
    {
        this.name = name;
        this.saver = saver;

        if (defaultSource != null && !saver.TryGetValue(name, out _) && saver.TryGetValue(defaultSource, out var existedDefault))
        {
            //デフォルトの参照元があり、値が格納されておらず、デフォルトの参照元が値を持っている場合
            value = Parse(existedDefault);
            saver.SetValue(name, Serialize(value));
        }
        else {
            value = Parse(saver.GetValue(name, Serialize(defaultValue)));
            if (Equals(value, defaultValue) && !shouldWrite) saver.RemoveValue(name);
        }
        saver.allEntries.Add(this);
    }

    public abstract T Parse(string str);
    protected virtual string Serialize(T value) => value.ToString()!;

    string IDataEntry.Serialize() => Serialize(value);
    void IDataEntry.DeserializeAndSetWithoutSave(string val) => SetValueWithoutSave(Parse(val));
    string IDataEntry.Id => name;
}

public class StringDataEntry : DataEntry<string>
{
    public override string Parse(string str) { return str; }
    protected override bool Equals(string val1, string val2) => val1 == val2;
    public StringDataEntry(string name, DataSaver saver, string defaultValue, string? defaultSource = null, bool shouldWrite = true) : base(name, saver, defaultValue, defaultSource, shouldWrite) { }
}

public class FloatDataEntry : DataEntry<float>
{
    public override float Parse(string str) { return float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0f; }
    protected override bool Equals(float val1, float val2) => val1 == val2;
    public FloatDataEntry(string name, DataSaver saver, float defaultValue, string? defaultSource = null, bool shouldWrite = true) : base(name, saver, defaultValue, defaultSource, shouldWrite) { }
}

public class ByteDataEntry : DataEntry<byte>
{
    public override byte Parse(string str) { return byte.TryParse(str, out var result) ? result : (byte)0; }
    protected override bool Equals(byte val1, byte val2) => val1 == val2;
    public ByteDataEntry(string name, DataSaver saver, byte defaultValue, string? defaultSource = null, bool shouldWrite = true) : base(name, saver, defaultValue, defaultSource, shouldWrite) { }
}

public class IntegerDataEntry : DataEntry<int>
{
    public override int Parse(string str) { return int.TryParse(str, out var result) ? result : 0; }
    protected override bool Equals(int val1, int val2) => val1 == val2;
    public IntegerDataEntry(string name, DataSaver saver, int defaultValue, string? defaultSource = null, bool shouldWrite = true) : base(name, saver, defaultValue, defaultSource, shouldWrite) { }
}

public class BooleanDataEntry : DataEntry<bool>
{
    public override bool Parse(string str) { return bool.TryParse(str, out var result) ? result : false; }
    protected override bool Equals(bool val1, bool val2) => val1 == val2;
    public BooleanDataEntry(string name, DataSaver saver, bool defaultValue, string? defaultSource = null, bool shouldWrite = true) : base(name, saver, defaultValue, defaultSource, shouldWrite) { }
}

public class IntegerTupleAryDataEntry : DataEntry<(int,int)[]>
{
    public override (int,int)[] Parse(string str) {
        if (str == "Empty") return new (int, int)[0];

        var strings = str.Split('|');
        (int, int)[] result = new (int, int)[strings.Length];
        for(int i = 0; i < result.Length; i++)
        {
            var tuple = strings[i].Split(',');
            result[i] = (int.Parse(tuple[0]), int.Parse(tuple[1]));
        }
        return result;
    }

    protected override string Serialize((int, int)[] value)
    {
        if (value.Length == 0) return "Empty";
        StringBuilder builder = new();
        foreach(var tuple in value)
        {
            if(builder.Length>0)builder.Append('|');
            builder.Append(tuple.Item1 + ',' + tuple.Item2);
        }
        return builder.ToString();
    }
    protected override bool Equals((int, int)[] val1, (int, int)[] val2) => false;
    public IntegerTupleAryDataEntry(string name, DataSaver saver, (int,int)[] defaultValue) : base(name, saver, defaultValue) { }
}

public class StringTupleAryDataEntry : DataEntry<(string, string)[]>
{
    public override (string, string)[] Parse(string str)
    {
        if (str == "Empty") return new (string, string)[0];

        var strings = str.Split('|');
        (string, string)[] result = new (string, string)[strings.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var tuple = strings[i].Split(',');
            result[i] = (tuple[0], tuple[1]);
        }
        return result;
    }

    protected override string Serialize((string, string)[] value)
    {
        if (value.Length == 0) return "Empty";
        StringBuilder builder = new();
        foreach (var tuple in value)
        {
            if (builder.Length > 0) builder.Append('|');

            builder.Append(tuple.Item1 + ',' + tuple.Item2);
        }
        return builder.ToString();
    }
    protected override bool Equals((string, string)[] val1, (string, string)[] val2) => false;

    public StringTupleAryDataEntry(string name, DataSaver saver, (string, string)[] defaultValue) : base(name, saver, defaultValue) { }
}

public class StringArrayDataEntry : DataEntry<string[]>
{
    public override string[] Parse(string str)
    {
        if (str == "Empty") return new string[0];

        return str.Split('|');
    }

    protected override string Serialize(string[] value)
    {
        if (value.Length == 0) return "Empty";
        StringBuilder builder = new();
        foreach (var elem in value)
        {
            if (builder.Length > 0) builder.Append('|');

            builder.Append(elem);
        }
        return builder.ToString();
    }
    protected override bool Equals(string[] val1, string[] val2) => false;

    public StringArrayDataEntry(string name, DataSaver saver, string[] defaultValue) : base(name, saver, defaultValue) { }
}

public class DataSaveSegment : IDisposable
{
    private static DataSaveSegment? CurrentSegment = null;
    private HashSet<DataSaver> SaveQueue = new();

    public DataSaveSegment()
    {
        if (CurrentSegment == null) CurrentSegment = this;
    }

    void IDisposable.Dispose()
    {
        if(CurrentSegment == this)
        {
            CurrentSegment = null;
            SaveQueue.Do(s => s.WriteToFile());
        }
    }

    public static void TrySave(DataSaver saver)
    {
        if(CurrentSegment == null)
        {
            saver.WriteToFile();
        }
        else
        {
            CurrentSegment.SaveQueue.Add(saver);
        }
    }
}
public class DataSaver
{
    private Dictionary<string, string> contents = new();
    internal List<IDataEntry> allEntries = new();
    string filename;

    public IEnumerable<(string,string)> AllRawContents()
    {
        foreach (var entry in contents) yield return (entry.Key, entry.Value);
    }
    public string GetValue(string name, object defaultValue)
    {
        if (contents.TryGetValue(name, out string? value))
        {
            return value!;
        }
        var res = contents[name] = defaultValue.ToString()!;
        return res;
    }

    public bool TryGetValue(string name, [MaybeNullWhen(false)]out string val) => contents.TryGetValue(name, out val);

    public void SetValue(string name, object value, bool skipSave = false)
    {
        contents[name] = value.ToString()!;
        if (!skipSave) TrySave();
    }

    public void RemoveValue(string name)
    {
        contents.Remove(name);
    }

    public DataSaver(string filename)
    {
        this.filename = filename;
        Load();
    }

    public static bool ExistData(string filename) => FileIO.Exists(ToDataSaverPath(filename));

    public static string ToDataSaverPath(string filename) => FileIO.GetDataPathTo(new string[] { "NebulaOnTheShip\\" + filename + ".dat" });

    public void Load()
    {
        string dataPathTo = ToDataSaverPath(filename);

        if (!FileIO.Exists(dataPathTo)) return;
        
        string[] vals = (FileIO.ReadAllText(dataPathTo)).Split("\n");
        foreach (string val in vals)
        {
            string[] str = val.Split(":", 2);
            if (str.Length != 2) continue;
            contents[str[0]] = str[1];
        }
    }

    public void WriteToFile()
    {
        string strContents = "";
        foreach (var entry in contents)
        {
            strContents += entry.Key + ":" + entry.Value + "\n";
        }
        try
        {
            FileIO.WriteAllText(ToDataSaverPath(filename), strContents);
        }catch (Exception e)
        {
            Debug.LogError($"DataOutputError ({filename})");
        }
    }

    public void TrySave()
    {
        DataSaveSegment.TrySave(this);
    }

    public IEnumerable<(string,string)> AllEntries()
    {
        foreach(var entry in contents)
        {
            yield return (entry.Key, entry.Value);
        }
    }

    public bool TryGetEntry(string key, [MaybeNullWhen(false)]out IDataEntry entry) =>  allEntries.Find(e => e.Id == key, out entry);
}

public class JsonDataSaver<T> where T : class, new()
{
    public T? Data { get; private set; } = new();
    string filename { get; set; }

    public JsonDataSaver(string filename)
    {
        this.filename = filename;
        Load();
    }

    public void Save()
    {
        string json = JsonStructure.Serialize(Data);
        FileIO.WriteAllText(DataSaver.ToDataSaverPath(filename), json);
    }

    public void Load()
    {
        string dataPathTo = DataSaver.ToDataSaverPath(filename);

        if (!FileIO.Exists(dataPathTo)) return;

        Data = JsonStructure.Deserialize<T>(FileIO.ReadAllText(dataPathTo));
    }
}
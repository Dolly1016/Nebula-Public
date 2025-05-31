using Cpp2IL.Core.Extensions;
using System.Text;
using Virial.Runtime;

namespace Nebula.Modules;

public interface IConfigPreset
{
    static protected readonly Dictionary<string, IConfigPreset> allPresets = new();
    static public IEnumerable<IConfigPreset> AllPresets => allPresets.Values;

    bool LoadPreset();
    string Id { get; }
    string DisplayName { get; }
    string? Detail { get; }
    string? RelatedHolder { get; }
    bool IsHidden { get; }
    bool IsOldType => false;
    bool IsUnknownType => false;
}

public class ScriptPreset : IConfigPreset{
    Action onLoad;
    public string Id { get; private set; }
    public string DisplayName { get; private set; }
    public string? Detail { get; private set; }
    public string? RelatedHolder { get; private set; }
    public bool IsHidden => false;


    public ScriptPreset(string id, string displayName, string? detail, string? relatedHolder, Action onLoad)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Detail = detail;
        this.RelatedHolder = relatedHolder;
        this.onLoad = onLoad;

        IConfigPreset.allPresets.Add(id,this);
        
    }

    bool IConfigPreset.LoadPreset()
    {
        try
        {
            onLoad.Invoke();
        }catch(Exception ex)
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, ex.ToString());
            return false;
        }
        return true;
        
    }
    
}
[NebulaPreprocess(PreprocessPhase.Roles)]
public class ConfigPreset : IConfigPreset
{
    public NebulaAddon? Addon => addon;
    NebulaAddon? addon;
    string path;
    public string Name { get; private set; }
    public string? displayName;
    public string? detail = null;
    public string? relatedHolder = null;
    public int version { get; private set; } = 1;
    public bool IsHidden { get;private set; }
    string IConfigPreset.Id => Name;
    public string DisplayName => displayName ?? Name;
    public string? Detail => detail;
    public string? RelatedHolder => relatedHolder;

    bool IConfigPreset.IsOldType => version >= 0 && version < 2;
    bool IConfigPreset.IsUnknownType => version < 0;

    public ConfigPreset(NebulaAddon? addon,string path, string name)
    {
        this.addon = addon;
        this.path = path;
        this.Name = name;

        try
        {
            ReadHeader();
            IConfigPreset.allPresets[name] = this;
        }
        catch
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, $"Preset \"{name}\" is invalid preset file.");
        }
    }

    //ローカルのプリセットを再読み込みします
    public static void LoadLocal(bool reload = true)
    {
        if(reload) foreach (var entry in IConfigPreset.allPresets) if (entry.Value is ConfigPreset cp && cp.addon == null) IConfigPreset.allPresets.Remove(entry.Key);

        string presetsFolder = "Presets";
        Directory.CreateDirectory(presetsFolder);
        foreach (var fullName in Directory.GetFiles(presetsFolder))
        {
            if (fullName.EndsWith(".dat"))
            {
                string path = fullName.Substring(presetsFolder.Length + 1);
                new ConfigPreset(null, path, System.IO.Path.GetFileNameWithoutExtension(path));
            }
        }

    }

    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Presets");

        foreach (var addon in NebulaAddon.AllAddons)
        {
            string Prefix = addon.InZipPath + "Presets/";
            foreach (var entry in addon.Archive.Entries)
            {
                if (entry.FullName.StartsWith(Prefix) && entry.FullName.EndsWith(".dat"))
                {
                    string path = entry.FullName.Substring(Prefix.Length);
                    new ConfigPreset(addon, path, System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }
        }

        LoadLocal(false);
    }

    private string? ReadString()
    {
        try
        {
            Stream? stream = addon != null ? addon.OpenStream("Presets/" + path) : File.OpenRead("Presets/" + path);

            if (stream != null) return new StreamReader(stream, Encoding.UTF8).ReadToEnd();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void ReadHeader()
    {
        var str = ReadString();

        if (str == null) return;

        var codes = str.Split('\n');

        foreach(var code in codes)
        {
            if (!code.StartsWith("#")) break;

            var args = code.Split(':');

            switch (args[0].ToUpper())
            {
                case "#DISPLAY":
                    if (args.Length > 1) args[1] = string.Join(':', args.Skip(1));
                    displayName = args[1];
                    break;
                case "#DETAIL":
                    if (args.Length > 1) args[1] = string.Join(':', args.Skip(1));
                    detail = args[1];
                    break;
                case "#HIDDEN":
                    IsHidden = true;
                    break;
                case "#RELATED":
                    relatedHolder = args[1];
                    break;
                case "#VERSION":
                    version = int.TryParse(args[1],out var v) ? v : -1;
                    break;
            }
        }

    }

    public bool LoadPreset() => LoadPreset(true);

    public bool LoadPreset(bool share)
    {
        try
        {
            var str = ReadString();

            if (str == null) return false;

            var codes = str.Split('\n');


            foreach (var code in codes)
            {
                if (code.StartsWith("//")) continue;


                var args = code.Split(':');
                for (int i = 0; i < args.Length; i++) args[i] = args[i].Trim();

                switch (args[0].ToUpper())
                {
                    case "SET":
                        try
                        {
                            if (args[2].StartsWith("'") && args[2].EndsWith("'")) args[2] = args[2].Substring(1, args[2].Length - 2);

                            if (args[1].StartsWith("vanilla."))
                            {
                                AmongUsUtil.ChangeOptionAs(args[1], args[2]);
                            }
                            else
                            {
                                ConfigurationValues.ConfigurationSaver.SetValue(args[1], args[2], true);
                            }
                        }
                        catch
                        {
                            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Preset, "Load Failed: " + args[1]);
                        }
                        break;
                }
            }

            foreach (var entry in ConfigurationValues.ConfigurationSaver.allEntries)
            {
                if (ConfigurationValues.ConfigurationSaver.TryGetValue(entry.Id, out var val)) entry.DeserializeAndSetWithoutSave(val);
            }
            ConfigurationValues.RestoreAll();

            if (share) ConfigurationValues.ShareAll();

            return true;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.ToString());
            return false;
        }
    }

    static public string ToDisplayNameText(string name) => "#DISPLAY:" + name;
    static private string OutputCurrentSettings(string name)
    {
        string result = ToDisplayNameText(name) + "\n#VERSION:2";

        foreach (var option in AmongUsUtil.AllVanillaOptions)
            result += "\nSET:" + option + ":'" + AmongUsUtil.GetOptionAsString(option) + "'";

        bool error = false;
        foreach (var option in ConfigurationValues.ConfigurationSaver.AllEntries())
        {
            result += "\nSET:" + option.Item1 + ":'" + option.Item2 + "'";
        }

        //1回でもエラーが起きていれば例外を投げる (出せるだけエラーを出し切ってから投げる)
        if (error) throw new Exception();

        return result;
    }

    static public bool OutputAndReloadSettings(string name, string? displayName = null)
    {
        try
        {
            File.WriteAllText(GetPathFromId(name), OutputCurrentSettings(displayName ?? name), Encoding.UTF8);
            LoadLocal();
        }catch(Exception)
        {
            return false;
        }
        return true;
    }

    static public string GetPathFromId(string id) => "Presets/" + id + ".dat";
    static public bool DeleteAndReloadSettings(string name)
    {
        File.Delete(GetPathFromId(name));
        LoadLocal();
        return true;
    }

    static public string[]? ReadRawPreset(string name)
    {
        string path = GetPathFromId(name);
        if (File.Exists(path))
        {
            return File.ReadAllLines(path);
        }
        return null;
    }
}

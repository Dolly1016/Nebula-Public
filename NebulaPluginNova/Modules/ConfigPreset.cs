using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules;

[NebulaPreLoad]
public class ConfigPreset
{
    static Dictionary<string, ConfigPreset> allPresets = new();
    static public IEnumerable<ConfigPreset> AllPresets => allPresets.Values;

    NebulaAddon? addon;
    string path;
    public string Name { get; private set; }
    public string? displayName;
    public string? detail = null;
    public string? relatedHolder = null;
    public bool IsHidden { get;private set; }
    public string DisplayName => displayName ?? Name;
    public string? Detail => detail;
    public string? RelatedHolder => relatedHolder;

    public ConfigPreset(NebulaAddon? addon,string path, string name)
    {
        this.addon = addon;
        this.path = path;
        this.Name = name;

        try
        {
            ReadHeader();
            allPresets[name] = this;
        }
        catch
        {
            NebulaPlugin.Log.Print(null, $"Preset \"{name}\" is invalid preset file.");
        }
    }

    public static void LoadLocal(bool reload = true)
    {
        if(reload) foreach (var entry in allPresets) if (entry.Value.addon == null) allPresets.Remove(entry.Key);

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

    public static IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Loading Presets";
        yield return null;

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
            }
        }

    }
    public bool LoadPreset(FunctionalEnvironment table,bool share = true)
    {
        try
        {
            table.Arguments["Players"] = IFunctionalVariable.Generate(PlayerControl.AllPlayerControls.Count);
        }
        catch { }

        try
        {
            var str = ReadString();

            if (str == null) return false;

            var codes = str.Split('\n');

            //Ifステートメントの履歴 0:真となる条件式なし　1:IF文中にいる　2:IF文脱出済み
            List<int> IfHistory = new();
            bool ShouldSkip() => IfHistory.Count > 0 && IfHistory[IfHistory.Count - 1] != 1;
            int GetIfState() => IfHistory.Count == 1 ? -1 : IfHistory[IfHistory.Count - 1];

            foreach (var code in codes)
            {
                if (code.StartsWith("//")) continue;


                var args = code.Split(':');
                for (int i = 0; i < args.Length; i++) args[i] = args[i].Trim();


                switch (args[0].ToUpper())
                {
                    case "IF":
                        if (ShouldSkip())
                        {
                            IfHistory.Add(2);
                            break;
                        }

                        IfHistory.Add(0);
                        if (args.Length != 2) break;
                        if (table.GetValue(args[1]).AsBool()) IfHistory[IfHistory.Count - 1] = 1;
                        break;
                    case "ELIF":
                    case "ELSEIF":
                        if (IfHistory.Count == 0) IfHistory.Add(0);
                        if (GetIfState() == 0)
                        {
                            if (table.GetValue(args[1]).AsBool()) IfHistory[IfHistory.Count - 1] = 1;
                        }
                        else
                        {
                            IfHistory[IfHistory.Count - 1] = 2;
                        }
                        break;
                    case "ELSE":
                        if (IfHistory.Count == 0) IfHistory.Add(0);
                        if (GetIfState() == 0)
                            IfHistory[IfHistory.Count - 1] = 1;
                        else
                            IfHistory[IfHistory.Count - 1] = 2;
                        break;
                    case "ENDIF":
                        if (IfHistory.Count > 0) IfHistory.RemoveAt(IfHistory.Count - 1);
                        break;
                    case "SET":
                        if (ShouldSkip()) break;
                        if (args[1].StartsWith("vanilla."))
                        {
                            AmongUsUtil.ChangeOptionAs(args[1], table.GetValue(args[2]).AsString());
                        }
                        else
                        {
                            var option = NebulaConfiguration.AllConfigurations.FirstOrDefault(option => option.Id == args[1]);
                            if (option != null) option.ChangeAs(table.GetValue(args[2]).AsString(), false);
                        }
                        break;
                    case "SUBSTITUTE":
                        table.Arguments[args[1]] = table.GetValue(args[2]);
                        break;
                    case "QUOTE":
                        if (allPresets.TryGetValue(table.GetValue(args[1]).AsString(), out var preset))
                        {
                            Dictionary<string, string> rawTable = new();
                            for (int i = 2; i < args.Length; i++) rawTable["#ARG" + (i - 2).ToString()] = args[i];
                            preset.LoadPreset(new(rawTable, table), false);
                        }
                        break;
                    case "UNPACK":
                        for (int i = 1; i < args.Length; i++) table.Arguments[args[i]] = table.GetValue("#ARG" + (i - 1).ToString());
                        break;
                }
            }

            if (share) NebulaConfigEntryManager.ShareAll();

            return true;
        }
        catch
        {
            return false;
        }
    }

    static private string OutputCurrentSettings(string name)
    {
        string result = "#DISPLAY:" + name;

        foreach (var option in AmongUsUtil.AllVanillaOptions)
            result += "\nSET:" + option + ":'" + AmongUsUtil.GetOptionAsString(option) + "'";
        
        foreach(var option in NebulaConfiguration.AllConfigurations)
        {
            result += "\nSET:" + option.Id + ":'" + (option.MaxValue >= 128 ? option.CurrentValue.ToString() : (option.GetMapped()?.ToString() ?? "null")) + "'";
        }

        return result;
    }

    static public void OutputAndReloadSettings(string name)
    {
        File.WriteAllText("Presets/" + name + ".dat", OutputCurrentSettings(name), Encoding.UTF8);
        LoadLocal();
    }
}

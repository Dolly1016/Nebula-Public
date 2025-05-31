using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Generator.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static System.Net.WebRequestMethods;

namespace NebulaLoader;


class ReleaseContent
{
    public string? name { get; set; } = null!;
}

[BepInPlugin("jp.dreamingpig.amongus.nebula.loader", "NebulaLoader", "1.0.0")]
[BepInProcess("Among Us.exe")]
public class NebulaLoader : BasePlugin
{
    private long GetVanillaSize()
    {
        if (!System.IO.File.Exists("GameAssembly.dll")) return -1;

        FileInfo file = new FileInfo("GameAssembly.dll");
        return file.Length;
    }

    static string GetTagsUrl(int page) => ConvertUrl("https://api.github.com/repos/Dolly1016/Nebula/tags?per_page=100&page=" + (page));
    private async Task<List<(string Tag, string Category, int Epoch, int Build, string VisualName)>> FetchAsync(HttpClient http)
    {
        List<(string Tag, string Category, int Epoch, int Build, string VisualName)> releases = new();

        int page = 1;
        while (true)
        {
            var response = await http.GetAsync(GetTagsUrl(page));

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Log.LogError("Bad Response: " + response.StatusCode.ToString());
                break;
            }

            string json = await response.Content.ReadAsStringAsync();
            
            var tags = JsonSerializer.Deserialize<ReleaseContent[]>(json);
            
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (tag.name != null)
                    {

                        string[] strings = tag.name.Split(",");
                        if (strings.Length != 4) continue;

                        if (!int.TryParse(strings[2], out var epoch)) continue;
                        if (!int.TryParse(strings[3], out var build)) continue;
                        releases.Add(new(tag.name, strings[0], epoch, build, strings[1]));
                    }
                }
            }
            if (tags == null || tags.Length == 0) break;
            page++;
        }

        releases.Sort((v1, v2) => v1.Epoch != v2.Epoch ? v2.Epoch - v1.Epoch : v2.Build - v1.Build);
        return releases;
    }

    private async Task UpdateAsync(HttpClient http, string tag, string dllFilePath)
    {
        string url = ConvertUrl($"https://github.com/Dolly1016/Nebula/releases/download/{tag}/Nebula.dll");
        var response = await http.GetAsync(url);
        if (response.StatusCode != HttpStatusCode.OK) return;
        var dllStream = await response.Content.ReadAsStreamAsync();

        try
        {
            if(System.IO.File.Exists(dllFilePath)) System.IO.File.Move(dllFilePath, dllFilePath + ".old", true);
            using var fileStream = System.IO.File.Create(dllFilePath);
            dllStream.CopyTo(fileStream);
            fileStream.Flush();
            UpdateIsDone = true;
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }
    }

    private async Task<List<(int Epoch, long Size)>> GetAssemblyInfoAsync(HttpClient http)
    {
        string url = ConvertUrl("https://raw.githubusercontent.com/Dolly1016/Nebula/master/epoch.dat");
        var response = await http.GetAsync(url);
        if (response.StatusCode != HttpStatusCode.OK) return [];
        string result = await response.Content.ReadAsStringAsync();
        var strings = result.Replace("\r\n","\n").Split('\n');

        List<(int Epoch, long Size)> list = new();
        foreach (var s in strings)
        {
            var splited = s.Split(',');
            if(splited.Length != 2) continue;
            if (int.TryParse(splited[0], out var epoch) && long.TryParse(splited[1], out var size)) list.Add((epoch, size));
        }
        return list;
    }

    static public ConfigEntry<bool> SkipCheckingConsistency { get; private set; } = null!;
    static public ConfigEntry<bool> IgnoringVersionConsistencyOnUpdate { get; private set; } = null!;
    static public ConfigEntry<bool> UseSnapshot { get; private set; } = null!;
    static public ConfigEntry<bool> AutoUpdate { get; private set; } = null!;
    static public ConfigEntry<bool> AllowHttpCommunication { get; private set; } = null!;
    static public ConfigEntry<bool> AutoUpdateIfVersionMismatch { get; private set; } = null!;
    static public ConfigEntry<string> UrlConverter { get; private set; } = null!;
    static public bool UpdateIsDone = false;

    static public string ConvertUrl(string url)
    {
        var converter = UrlConverter.Value;
        if(converter.Length <= 1) return url;
        else return UrlConverter.Value.Replace("<url>", url);
    }
    static private NebulaLoader MyPlugin = null!;
    public override void Load()
    {
        MyPlugin = this;
        SkipCheckingConsistency = Config.Bind("Options", "SkipCheckingConsistency", false, "When enabled, All checking routines will be skipped.");
        IgnoringVersionConsistencyOnUpdate = Config.Bind("Options", "IgnoringVersionConsistency", false, "When enabled, this allows for combinations of NoS and Among Us versions that are not guaranteed.");
        UseSnapshot = Config.Bind("Options", "UseSnapshot", false, "When enabled, Get the latest snapshot or stable version.");
        AutoUpdate = Config.Bind("Options", "AutoUpdate", false, "When enabled, the automatic update feature is enabled.");
        AutoUpdateIfVersionMismatch = Config.Bind("Options", "AutoUpdateIfVersionMismatching", true, "Automatically updates when a version mismatch of Among Us is detected. This setting is ignored when AutoUpdate is enabled.");
        UrlConverter = Config.Bind("Options", "UrlConverter", "<url>", "You can convert the URL used by NoS to access the Internet into a specified format.");
        AllowHttpCommunication = Config.Bind("Options", "AllowHttpCommunication", true, "In environments with limited internet access, setting this option to false may allow the game to run.");

        bool autoUpdate = AutoUpdate.Value;
        bool autoUpdateIfVersionMismatch = AutoUpdateIfVersionMismatch.Value;
        string dllDirectoryPath = "BepInEx" + Path.DirectorySeparatorChar + "nebula";
        string dllFilePath = dllDirectoryPath + Path.DirectorySeparatorChar + "Nebula.dll";

        if (!SkipCheckingConsistency.Value)
        {

            if (!System.IO.File.Exists(dllFilePath)) autoUpdate = true;

            if (autoUpdate || autoUpdateIfVersionMismatch)
            {

                long size = GetVanillaSize();
                if (size == -1)
                {
                    Log.LogWarning("Assembly Is Not Found.\nAttempts to load an existing NoS.");
                    TryLoadNebula(dllFilePath);
                    return;
                }
                Log.LogInfo("Assembly Size: " + size.ToString());

                HttpClient http = new();
                http.DefaultRequestHeaders.Add("User-Agent", "Nebula Updater");

                var assemblyInfoTask = GetAssemblyInfoAsync(http);
                Log.LogInfo("Start getting information about the Among us assembly...");
                assemblyInfoTask.Wait();
                var assemblyCandidates = assemblyInfoTask.Result.Where(tuple => tuple.Size == size).Select(tuple => tuple.Epoch).Distinct().ToArray();

                if (assemblyCandidates.Length == 0 && !IgnoringVersionConsistencyOnUpdate.Value)
                {
                    Log.LogWarning("Unknown assembly detected.\nAttempts to load an existing NoS.");
                    TryLoadNebula(dllFilePath);
                    return;
                }

                if (assemblyCandidates.Length == 1) Log.LogInfo("Detected Epoch: " + assemblyCandidates[0]);

                var allVersions = FetchAsync(http);
                allVersions.Wait();

                Log.LogInfo("Releases Count: " + allVersions.Result.Count);
                Log.LogInfo("Version Matched Releases Count: " + allVersions.Result.Count(v => assemblyCandidates.Contains(v.Epoch)));

                var candidates = allVersions.Result.Where(v => (IgnoringVersionConsistencyOnUpdate.Value || assemblyCandidates.Contains(v.Epoch)) && (v.Category == "v" || (UseSnapshot.Value && v.Category == "s"))).ToArray();

                if (candidates.Length == 0)
                {
                    Log.LogWarning("There is no NoS that can be implemented in the current environment.\nAttempts to load an existing NoS.");
                    TryLoadNebula(dllFilePath);
                    return;
                }

                Directory.CreateDirectory(dllDirectoryPath);

                bool shouldDownload = true;

                if (System.IO.File.Exists(dllFilePath))
                {
                    FileVersionInfo file = FileVersionInfo.GetVersionInfo(dllFilePath);
                    int currentEpoch = file.FileMajorPart;
                    int currentBuild = file.FileMinorPart;

                    if (candidates[0].Epoch == currentEpoch && candidates[0].Build == currentBuild)
                    {
                        Log.LogInfo("The latest NoS is already in place.");
                        shouldDownload = false;
                    }

                    //バージョン不一致時のみ更新する場合、バージョン候補内のエポックと現在のエポックが一致していれば何もしない。
                    if (!autoUpdate && candidates.Any(c => c.Epoch == currentEpoch))
                    {
                        shouldDownload = false;
                    }
                }

                if (shouldDownload)
                {
                    Log.LogInfo("Installing " + candidates[0].VisualName.Replace('_', ' ') + "...");
                    UpdateAsync(http, candidates[0].Tag, dllFilePath).Wait();
                }
            }
        }

        TryLoadNebula(dllFilePath);
    }

    static void TryLoadNebula(string dllFilePath)
    {
        string dllFullFilePath = System.IO.Path.GetFullPath(dllFilePath);
        if (System.IO.File.Exists(dllFullFilePath))
        {
            Assembly NebulaAssembly = Assembly.LoadFile(dllFullFilePath);

            var nebulaPluginType = NebulaAssembly.GetType("Nebula.NebulaPlugin");
            nebulaPluginType?.GetMethod("Load", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
            nebulaPluginType?.GetField("LoaderPlugin", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, MyPlugin);
        }
    }

    /*
    void RegisterErrorPopup()
    {
        SceneManager.sceneLoaded += (UnityAction<Scene, LoadSceneMode>)((scene, loadMode) =>
        {
            if (scene.name == "MainMenu") Log.LogInfo("TEST");
        });
    }
    */
}

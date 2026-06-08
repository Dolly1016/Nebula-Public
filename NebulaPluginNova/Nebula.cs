global using BepInEx.Unity.IL2CPP.Utils.Collections;
global using HarmonyLib;
global using Il2CppInterop.Runtime;
global using Nebula.Configuration;
global using Nebula.Extensions;
global using Nebula.Game;
global using Nebula.Modules;
global using Nebula.Modules.ScriptComponents;
global using Nebula.Player;
global using Nebula.Utilities;
global using System.Collections;
global using UnityEngine;
global using Virial.Attributes;
global using Virial.Helpers;
global using Virial.Utilities;
global using Color = UnityEngine.Color;
global using GamePlayer = Virial.Game.Player;
global using GUI = Nebula.Modules.GUIWidget.NebulaGUIWidgetEngine;
global using GUIWidget = Virial.Media.GUIWidget;
global using Image = Virial.Media.Image;
global using Timer = Nebula.Modules.ScriptComponents.TimerImpl;
using AmongUs.Data.Player;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Cpp2IL.Core.Extensions;
using Hazel.Udp;
using Interstellar;
using Nebula.Modules.CustomMap;
using Nebula.VisualProgramming;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Virial;
using Virial.VisualProgramming;

#if PC
[assembly: System.Reflection.AssemblyFileVersionAttribute(Nebula.NebulaPlugin.PluginEpochStr + "."  + Nebula.NebulaPlugin.PluginBuildNumStr)]
#endif
namespace Nebula;

public class NebulaPlugin
{
    public const string AmongUsVersion = "2023.7.12";
    public const string PluginGuid = "jp.dreamingpig.amongus.nebula";
    public const string PluginName = "NebulaOnTheShip";
    public const string PluginVersion = "3.3.0.0";

    public const string VisualVersion = "v3.3";
    //public const string VisualVersion = "Snapshot 26.06.07a";
    //public const string VisualVersion = "Addon Loading DEMO 2";

    public const string PluginEpochStr = "108";
    public const string PluginBuildNumStr = "1598";
    public static readonly int PluginEpoch = int.Parse(PluginEpochStr);
    public static readonly int PluginBuildNum = int.Parse(PluginBuildNumStr);
    public const bool GuardVanillaLangData = false;
    public static AssemblyLoadContext NoSAssemblyContext = new AssemblyLoadContext("NoSAssemblyContext");

    private static Dictionary<string, ConfigEntryBase> loaderConfigurations = [];

    internal static ConfigEntry<T>? GetLoaderConfig<T>(string name)
    {
#if PC
        if (loaderConfigurations.TryGetValue(name, out var entry)) return entry as ConfigEntry<T>;
        ConfigEntryBase? entryBase = typeof(NebulaLoader.NebulaLoader).GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)?.GetValue(null) as ConfigEntryBase;
        if (entryBase != null) loaderConfigurations[name] = entryBase;
        return entryBase as ConfigEntry<T>;
#else
        return null;
#endif
    }

#if PC
    internal static bool AllowHttpCommunication => NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.AllowHttpCommunication))?.Value ?? true;

    static public HttpClient HttpClient
    {
        get
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Nebula Updater");
            }
            return httpClient;
        }
    }
    static private HttpClient? httpClient = null;
#else
    internal static bool AllowHttpCommunication => true;
#endif


    public static string GetNebulaVersionString()
    {
        return "NoS " + VisualVersion;
    }

    static public Harmony Harmony = new Harmony(PluginGuid);


    public static bool IsPreferential => NebulaLogFile.IsPreferential;
    public static BasePlugin LoaderPlugin = null!;


    static public void LoadForAndroid()
    {
        LoadInternal(true);
    }

    static public void Load()
    {
        LoadInternal(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static private void LoadInterstellar()
    {
        InterstellarLoader.Load();
    }

    static public bool IsAndroid { get; private set; }
    
    static internal void LoadInternal(bool android)
    {
        IsAndroid = android;
        //if (!IsAndroid) System.Console.OutputEncoding = Encoding.UTF8;
        NebulaLogFile.Initialize(android);

        void LoadLibraryFromResource(string path, string dllName) {
            LoadLibrary(StreamHelper.OpenFromResource(path), dllName);
        }

        void LoadLibraryFromZip(string zipPath) {
            using var apiStream = StreamHelper.OpenFromResource(zipPath)!;
            using var zip = new ZipArchive(apiStream);
            foreach(var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith(".dll"))
                {
                    LoadLibrary(entry.Open(), entry.FullName);
                }
            }
        }

        void LoadLibrary(Stream dllStream, string dllName)
        {
            MD5 md5 = MD5.Create();
            var dirPath = PathHelpers.NebulaLibsPath;
            var filePath = dirPath + Path.DirectorySeparatorChar + dllName;
            var apiBytes = dllStream.ReadBytes();
            var apiHash = BitConverter.ToString(md5.ComputeHash(apiBytes));
            if (Directory.Exists(dirPath) && File.Exists(filePath) && BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(filePath))) == apiHash)
            {
                //pass
            }
            else
            {
                Directory.CreateDirectory(dirPath);
                File.WriteAllBytes(filePath, apiBytes);
            }
        }

        LoadLibraryFromZip("Nebula.Resources.Libs.zip");
        LoadLibraryFromResource("Nebula.Resources.Interstellar.dll", "Interstellar.dll");

#if PC
        Assembly.LoadFrom(Path.Combine(PathHelpers.NebulaLibsPath, "NebulaAPI.dll"));
#endif
#if ANDROID
        NebulaAndroid.CSScriptAndroid.AndroidCompilerSupport.AddDependencyResolver();
        Assembly.LoadFrom(Path.Combine(PathHelpers.NebulaLibsPath, "Microsoft.CodeAnalysis.dll"));
        Assembly.LoadFrom(Path.Combine(PathHelpers.NebulaLibsPath, "Microsoft.CodeAnalysis.CSharp.dll"));
#endif
        Assembly.LoadFrom(Path.Combine(PathHelpers.NebulaLibsPath, "Interstellar.dll"));

        LoadInterstellar();

        Harmony.PatchAll();

        //パッチが当たったメソッドの情報を表示
        /*
        foreach(var m in Harmony.GetPatchedMethods())
        {
            LogUtils.WriteToConsole(m.Name);
        }
        */

        bool isFirst = true;
        SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)((scene, loadMode) =>
        {
            new GameObject("NebulaManager").AddComponent<NebulaManager>();
            if (isFirst)
            {
                isFirst = false;
                var residentObj = new GameObject("ResidentObject");
                residentObj.AddComponent<ResidentBehaviour>().MarkDontUnload();
                residentObj.MarkDontUnload();
            }
        });

        SetUpNebulaImpl();
    }

    static private void SetUpNebulaImpl()
    {
        NebulaAPI.instance = new NebulaImpl();

        LogUtils.WriteToConsole($"Incremental GC: {GarbageCollector.isIncremental}");
        LogUtils.WriteToConsole($"GC time slice: {GarbageCollector.incrementalTimeSliceNanoseconds} ns");
        LogUtils.WriteToConsole($"GC time slice: {GarbageCollector.incrementalTimeSliceNanoseconds / 1_000_000.0} ms");
    }
}


[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Awake))]
public static class AmongUsClientAwakePatch
{
    public static bool IsFirstFlag = true;
    
    public static void Postfix(AmongUsClient __instance)
    {
        if (!IsFirstFlag) return;
        IsFirstFlag = false;

        Language.OnChangeLanguage((uint)AmongUs.Data.DataManager.Settings.Language.CurrentLanguage);

        __instance.StartCoroutine(VanillaAsset.CoLoadAssetOnTitle().WrapToIl2Cpp());
    }
}


[HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldFlipSkeld))]
public static class AprilFoolsModePatch
{
    public static bool Prefix(out bool __result)
    {
        __result = false;
        return false;
    }
}

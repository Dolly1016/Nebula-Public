global using BepInEx.Unity.IL2CPP.Utils.Collections;
global using Il2CppInterop.Runtime;
global using Nebula.Extensions;
global using Nebula.Utilities;
global using Nebula.Game;
global using Nebula.Player;
global using Nebula.Modules;
global using Nebula.Configuration;
global using UnityEngine;
global using Nebula.Modules.ScriptComponents;
global using System.Collections;
global using HarmonyLib;
global using Virial.Attributes;
global using Virial.Helpers;
global using Timer = Nebula.Modules.ScriptComponents.TimerImpl;
global using Color = UnityEngine.Color;
global using GUIWidget = Virial.Media.GUIWidget;
global using GUI = Nebula.Modules.GUIWidget.NebulaGUIWidgetEngine;
global using Image = Virial.Media.Image;
global using GamePlayer = Virial.Game.Player;


using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.Runtime.InteropServices;
using UnityEngine.SceneManagement;
using Virial;
using Cpp2IL.Core.Extensions;
using System.Reflection;
using System.Reflection.Metadata;
using Nebula.Modules.CustomMap;
using System.IO.Compression;
using Nebula.VisualProgramming;
using Virial.VisualProgramming;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Networking;
using Hazel.Udp;
using BepInEx.Configuration;

[assembly: System.Reflection.AssemblyFileVersionAttribute(Nebula.NebulaPlugin.PluginEpochStr + "."  + Nebula.NebulaPlugin.PluginBuildNumStr)]

namespace Nebula;

public class NebulaPlugin
{
    public const string AmongUsVersion = "2023.7.12";
    public const string PluginGuid = "jp.dreamingpig.amongus.nebula";
    public const string PluginName = "NebulaOnTheShip";
    public const string PluginVersion = "2.21.0.10";

    public const string VisualVersion = "v2.21.0.10";
    //public const string VisualVersion = "Snapshot 25.05.18b";
    //public const string VisualVersion = "Costume Animation DEMO 2";

    public const string PluginEpochStr = "107";
    public const string PluginBuildNumStr = "1388";
    public static readonly int PluginEpoch = int.Parse(PluginEpochStr);
    public static readonly int PluginBuildNum = int.Parse(PluginBuildNumStr);
    public const bool GuardVanillaLangData = false;

    private static Dictionary<string, ConfigEntryBase> loaderConfigurations = [];
    internal static ConfigEntry<T>? GetLoaderConfig<T>(string name)
    {
        if (loaderConfigurations.TryGetValue(name, out var entry)) return entry as ConfigEntry<T>;
        ConfigEntryBase? entryBase = typeof(NebulaLoader.NebulaLoader).GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)?.GetValue(null) as ConfigEntryBase;
        if (entryBase != null) loaderConfigurations[name] = entryBase;
        return entryBase as ConfigEntry<T>;
    }
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

    
    public static new NebulaLog Log { get; private set; } = new();


    public static string GetNebulaVersionString()
    {
        return "NoS " + VisualVersion;
    }

    static public Harmony Harmony = new Harmony(PluginGuid);


    public bool IsPreferential => Log.IsPreferential;
    public static NebulaPlugin MyPlugin { get; private set; } = null!;
    public static BasePlugin LoaderPlugin = null!;

    static public void Load()
    {
        Assembly.Load(StreamHelper.OpenFromResource("Nebula.Resources.API.NAudio.Core.dll")!.ReadBytes());
        Assembly.Load(StreamHelper.OpenFromResource("Nebula.Resources.API.NAudio.Wasapi.dll")!.ReadBytes());
        Assembly.Load(StreamHelper.OpenFromResource("Nebula.Resources.API.NAudio.WinMM.dll")!.ReadBytes());
        Assembly.Load(StreamHelper.OpenFromResource("Nebula.Resources.API.OpusDotNet.dll")!.ReadBytes());
        Assembly.Load(StreamHelper.OpenFromResource("Nebula.Resources.API.NebulaAPI.dll")!.ReadBytes());

        Harmony.PatchAll();

        //パッチが当たったメソッドの情報を表示
        /*
        foreach(var m in Harmony.GetPatchedMethods())
        {
            LogUtils.WriteToConsole(m.Name);
        }
        */

        SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)((scene, loadMode) =>
        {
            new GameObject("NebulaManager").AddComponent<NebulaManager>();
        });
        SetUpNebulaImpl();

        
    }

    static private void SetUpNebulaImpl()
    {
        NebulaAPI.instance = new NebulaImpl();
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

/*
[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.AmBanned), MethodType.Getter)]
public static class AmBannedPatch
{
    public static void Postfix(out bool __result)
    {
        __result = false;
    }
}
*/

[HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldFlipSkeld))]
public static class AmBannedPatch
{
    public static bool Prefix(out bool __result)
    {
        __result = false;
        return false;
    }
}

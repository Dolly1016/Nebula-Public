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
global using Timer = Nebula.Modules.ScriptComponents.Timer;
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

namespace Nebula;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("Among Us.exe")]
public class NebulaPlugin : BasePlugin
{
    public const string AmongUsVersion = "2023.7.12";
    public const string PluginGuid = "jp.dreamingpig.amongus.nebula";
    public const string PluginName = "NebulaOnTheShip";
    public const string PluginVersion = "2.2.3.1";

    //public const string VisualVersion = "v2.3";
    public const string VisualVersion = "Snapshot 24.05.28a";
    //public const string VisualVersion = "RPC Debug 2";

    public const int PluginEpoch = 103;
    public const int PluginBuildNum = 1124;
    public const bool GuardVanillaLangData = true;

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

    public Harmony Harmony = new Harmony(PluginGuid);

    [DllImport("user32.dll", EntryPoint = "SetWindowText")]
    public static extern bool SetWindowText(System.IntPtr hwnd, System.String lpString);
    [DllImport("user32.dll", EntryPoint = "FindWindow")]
    public static extern System.IntPtr FindWindow(System.String className, System.String windowName);

    public bool IsPreferential => Log.IsPreferential;
    public static NebulaPlugin MyPlugin { get; private set; } = null!;

    override public void Load()
    {
        MyPlugin = this;

        Harmony.PatchAll();

        SetWindowText(FindWindow(null!, Application.productName),"Among Us w/ " + GetNebulaVersionString());

        SceneManager.sceneLoaded += (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)((scene, loadMode) =>
        {
            new GameObject("NebulaManager").AddComponent<NebulaManager>();
        });

        SetUpNebulaImpl();
    }

    private void SetUpNebulaImpl()
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

[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.AmBanned), MethodType.Getter)]
public static class AmBannedPatch
{
    public static void Postfix(out bool __result)
    {
        __result = false;
    }
}

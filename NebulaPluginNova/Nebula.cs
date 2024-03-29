﻿global using BepInEx.Unity.IL2CPP.Utils.Collections;
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
global using Timer = Nebula.Modules.ScriptComponents.Timer;
global using Color = UnityEngine.Color;
global using GUIWidget = Virial.Media.GUIWidget;
global using GUI = Nebula.Modules.MetaWidget.NebulaGUIWidgetEngine;
global using Image = Virial.Media.Image;
global using GamePlayer = Virial.Game.Player;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.Runtime.InteropServices;
using UnityEngine.SceneManagement;
using System.Reflection;
using Cpp2IL.Core.Extensions;
using Virial;

namespace Nebula;

[NebulaPreLoad]
public static class ToolsInstaller
{
    public static IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Installing Tools";
        yield return null;

        InstallTool("VoiceChatSupport.exe");
        InstallTool("CPUAffinityEditor.exe");
        InstallTool("opus.dll");
    }
    private static void InstallTool(string name)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream? stream = assembly.GetManifestResourceStream("Nebula.Resources.Tools." + name);
        if (stream == null) return;

        var file = File.Create(name);
        byte[] data = new byte[stream.Length];
        stream.Read(data);
        file.Write(data);
        stream.Close();
        file.Close();
    }
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("Among Us.exe")]
public class NebulaPlugin : BasePlugin
{
    public const string AmongUsVersion = "2023.7.12";
    public const string PluginGuid = "jp.dreamingpig.amongus.nebula";
    public const string PluginName = "NebulaOnTheShip";
    public const string PluginVersion = "2.2.3.1";

    //public const string VisualVersion = "v2.2.3.1";
    public const string VisualVersion = "Snapshot 24.03.17a";
    //public const string VisualVersion = "RPC Debug 2";

    public const int PluginEpoch = 103;
    public const int PluginBuildNum = 1091;
    
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

    public static bool FinishedPreload { get; private set; } = false;

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
    public IEnumerator CoLoad()
    {
        yield return Preload();
        VanillaAsset.LoadAssetAtInitialize();
    }

    public static (Exception,Type)? LastException = null;
    private IEnumerator Preload()
    {
        void OnRaisedExcep(Exception exception,Type type)
        {
            LastException ??= (exception,type);
        }

        var types = Assembly.GetAssembly(typeof(RemoteProcessBase))?.GetTypes().Where((type) => type.IsDefined(typeof(NebulaPreLoad)));
        if (types != null)
        {
            List<Type> PostLoad = new();
            HashSet<Type> Loaded = new();

            IEnumerator Preload(Type type, bool isFinalize)
            {
                if (Loaded.Contains(type)) yield break;

                if (type.IsDefined(typeof(NebulaPreLoad)))
                {
                    var myPreType = type.GetCustomAttribute<NebulaPreLoad>()!;
                    var preTypes = myPreType.PreLoadType;
                    foreach (var pretype in preTypes) yield return Preload(pretype, isFinalize);
                    if (!isFinalize && myPreType.MyFinalizerType != NebulaPreLoad.FinalizerType.NotFinalizer)
                    {
                        if (myPreType.MyFinalizerType == NebulaPreLoad.FinalizerType.LoadOnly)
                        {
                            UnityEngine.Debug.Log("Preload (static constructor) " + type.Name);
                            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                        }

                        PostLoad.Add(type);
                        yield break;
                    }
                }

                UnityEngine.Debug.Log("Preload " + type.Name);

                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                var loadMethod = type.GetMethod("Load");
                if (loadMethod != null)
                {
                    try
                    {
                        loadMethod.Invoke(null, null);
                        UnityEngine.Debug.Log("Preloaded type " + type.Name + " has Load()");
                    }
                    catch(Exception e)
                    {
                        OnRaisedExcep(e,type);
                        //UnityEngine.Debug.Log("Preloaded type " + type.Name + " has Load with unregulated parameters.");
                    }
                }

                var coloadMethod = type.GetMethod("CoLoad");
                if (coloadMethod != null)
                {
                    IEnumerator? coload = null;
                    try
                    {
                        coload = (IEnumerator)coloadMethod.Invoke(null, null)!;
                        UnityEngine.Debug.Log("Preloaded type " + type.Name + " has CoLoad");
                    }
                    catch (Exception e)
                    {
                        OnRaisedExcep(e, type);
                        //UnityEngine.Debug.Log("Preloaded type " + type.Name + " has CoLoad with unregulated parameters.");
                    }
                    if (coload != null) yield return coload.HandleException((e)=>OnRaisedExcep(e,type));
                }

                Loaded.Add(type);
            }

            foreach (var type in types) yield return Preload(type, false);
            foreach (var type in PostLoad) yield return Preload(type, true);
        }
        FinishedPreload = true;
    }

    override public void Load()
    {
        MyPlugin = this;

        var assembly = Assembly.GetExecutingAssembly();
        Assembly.Load(assembly.GetManifestResourceStream("Nebula.Resources.API.NebulaAPI.dll").ReadBytes());

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

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class NebulaPreLoad : Attribute
{
    public Type[] PreLoadType { get; private set; }
    public FinalizerType MyFinalizerType { get; private set; }
    public NebulaPreLoad(params Type[] preLoadType)
    {
        PreLoadType = preLoadType;
        MyFinalizerType = FinalizerType.NotFinalizer;
    }

    public NebulaPreLoad(bool isFinalizer, params Type[] preLoadType)
    {
        PreLoadType = preLoadType;
        MyFinalizerType = isFinalizer ? FinalizerType.StaticConstAndLoad : FinalizerType.NotFinalizer;
    }

    public enum FinalizerType
    {
        LoadOnly,
        StaticConstAndLoad,
        NotFinalizer
    }

    public NebulaPreLoad(FinalizerType finalizerType, params Type[] preLoadType)
    {
        PreLoadType = preLoadType;
        MyFinalizerType = finalizerType;
    }

    static public bool FinishedLoading => NebulaPlugin.FinishedPreload;
}

[HarmonyPatch(typeof(StatsManager), nameof(StatsManager.AmBanned), MethodType.Getter)]
public static class AmBannedPatch
{
    public static void Postfix(out bool __result)
    {
        __result = false;
    }
}

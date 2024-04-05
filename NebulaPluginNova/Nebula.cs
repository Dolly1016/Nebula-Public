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
using Unity.IO.LowLevel.Unsafe;
using LibCpp2IL.PE;

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

    //public const string VisualVersion = "v2.3";
    public const string VisualVersion = "Snapshot 24.04.04a";
    //public const string VisualVersion = "RPC Debug 2";

    public const int PluginEpoch = 103;
    public const int PluginBuildNum = 1099;
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

        Patches.LoadPatch.LoadingText = "Checking Component Dependencies";
        yield return null;

        var types = Assembly.GetAssembly(typeof(RemoteProcessBase))?.GetTypes().Where((type) => type.IsDefined(typeof(NebulaPreLoad)));
        if (types != null)
        {
            Dictionary<Type, (Reference<int> leftPreLoad, HashSet<Type> postLoad, bool isFinalizer)> dependencyMap = new();

            foreach (var type in types) dependencyMap[type] = (new Reference<int>().Set(0), new(), type.GetCustomAttribute<NebulaPreLoad>()!.IsFinalizer);

            //有向グラフを作る
            foreach (var type in types) {
                var myAttr = type.GetCustomAttribute<NebulaPreLoad>()!;
                dependencyMap.TryGetValue(type, out var myInfo);

                foreach (var pre in myAttr.PreLoadTypes)
                {
                    if (dependencyMap.TryGetValue(pre, out var preInfo))
                    {
                        //NebulaPreLoadの対象の場合は順番を考慮する
                        if (preInfo.postLoad.Add(type))
                        {
                            myInfo.leftPreLoad.Update(v => v + 1);
                        }
                    }
                    else
                    {
                        //NebulaPreLoadの対象でない場合はそのまま読み込む
                        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(pre.TypeHandle);
                    }
                }

                foreach(var post in myAttr.PostLoadTypes)
                {
                    if (dependencyMap.TryGetValue(post, out var postInfo))
                    {
                        //NebulaPreLoadの対象の場合は順番を考慮する
                        if (myInfo.postLoad.Add(type)) postInfo.leftPreLoad.Update(v => v + 1);
                    }
                    //NebulaPreLoadの対象でない場合はなにもしない
                }
            }

            Queue<Type> waitingList = new(dependencyMap.Where(tuple => tuple.Value.leftPreLoad.Value == 0 && !tuple.Value.isFinalizer).Select(t => t.Key));
            Queue<Type> waitingFinalizerList = new(dependencyMap.Where(tuple => tuple.Value.leftPreLoad.Value == 0 && tuple.Value.isFinalizer).Select(t => t.Key));

            //読み込み順リスト
            List<Type> loadList = new();

            while (waitingList.Count > 0 || waitingFinalizerList.Count > 0)
            {
                var type = (waitingList.Count > 0 ? waitingList : waitingFinalizerList).Dequeue();

                loadList.Add(type);
                foreach(var post in dependencyMap[type].postLoad)
                {
                    if(dependencyMap.TryGetValue(post, out var postInfo))
                    {
                        postInfo.leftPreLoad.Update(v => v - 1);
                        if (postInfo.leftPreLoad.Value == 0) (postInfo.isFinalizer ? waitingFinalizerList : waitingList).Enqueue(post);
                    }
                }
            }

            //解決状況を出力
            var stringList = loadList.Join(t => "  -" + t.FullName, "\n");
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, "Dependencies resolved sequentially.\n" + stringList);

            if(loadList.Count < dependencyMap.Count)
            {
                var errorStringList = dependencyMap.Where(d => d.Value.leftPreLoad.Value > 0).Join(t => "  -" + t.Key.FullName, "\n");
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Components that could not be resolved.\n" + errorStringList);

                throw new Exception("Failed to resolve dependencies.");
            }

            IEnumerator Preload(Type type)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                var loadMethod = type.GetMethod("Load");
                if (loadMethod != null)
                {
                    try
                    {
                        loadMethod.Invoke(null, null);
                    }
                    catch(Exception e)
                    {
                        OnRaisedExcep(e,type);
                        NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, null, "Preloaded type " + type.Name + " has Load with unregulated parameters.");
                    }
                }

                var coloadMethod = type.GetMethod("CoLoad");
                if (coloadMethod != null)
                {
                    IEnumerator? coload = null;
                    try
                    {
                        coload = (IEnumerator)coloadMethod.Invoke(null, null)!;
                    }
                    catch (Exception e)
                    {
                        OnRaisedExcep(e, type);
                        NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, null, "Preloaded type " + type.Name + " has CoLoad with unregulated parameters.");
                    }
                    if (coload != null) yield return coload.HandleException((e)=>OnRaisedExcep(e,type));
                }
            }

            foreach (var type in loadList) yield return Preload(type);
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
    public Type[] PreLoadTypes { get; private set; }
    public Type[] PostLoadTypes { get; private set; }
    public bool IsFinalizer { get; private set; }
    public NebulaPreLoad(params Type[] preLoadType) : this(false, preLoadType, []) { }
    public NebulaPreLoad(bool isFinalizer, params Type[] preLoadType) :this(isFinalizer, preLoadType, []){ }

    public NebulaPreLoad(bool isFinalizer, Type[] preLoad, Type[] postLoad)
    {
        PreLoadTypes = preLoad ?? [];
        PostLoadTypes = postLoad ?? [];
        IsFinalizer = isFinalizer;
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

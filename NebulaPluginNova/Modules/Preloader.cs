using Nebula.Behaviour;
using System.Reflection;
using Virial.DI;
using Virial.Game;

namespace Nebula.Modules;

[NebulaPreLoad]
public static class ToolsInstaller
{
    public static IEnumerator CoLoad()
    {
        if (NebulaPlugin.Log.IsPreferential)
        {
            Patches.LoadPatch.LoadingText = "Installing Tools";
            yield return null;

            InstallTool("CPUAffinityEditor.exe");
            InstallTool("opus.dll");
        }
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

public static class PreloadManager
{
    static public IEnumerator CoLoad()
    {
        yield return Preload();
        VanillaAsset.LoadAssetAtInitialize();
    }

    public static (Exception, Type)? LastException = null;
    static private IEnumerator Preload()
    {
        DIManager.Instance.RegisterContainer(()=>new NebulaGameManager());
        DIManager.Instance.RegisterContainer(() => new PlayerModInfo());

        //IModule<Virial.Game.Game>
        DIManager.Instance.RegisterGeneralModule<Virial.Game.Game>(() => GeneralConfigurations.CurrentGameMode.InstantiateModule());
        DIManager.Instance.RegisterModule(() => new Synchronizer());
        DIManager.Instance.RegisterModule(() => new MeetingPlayerButtonManager());
        DIManager.Instance.RegisterModule(() => new MeetingOverlayHolder());
        DIManager.Instance.RegisterModule(() => new TitleShower());
        DIManager.Instance.RegisterModule(() => new PerkHolder());
        DIManager.Instance.RegisterModule(() => new FakeInformation());

        //IModule<Virial.Game.Player>
        DIManager.Instance.RegisterModule(() => new PlayerTaskState());

        void OnRaisedExcep(Exception exception, Type type)
        {
            LastException ??= (exception, type);
        }

        Patches.LoadPatch.LoadingText = "Checking Component Dependencies";
        yield return null;

        var types = Assembly.GetAssembly(typeof(RemoteProcessBase))?.GetTypes().Where((type) => type.IsDefined(typeof(NebulaPreLoad)));
        if (types != null)
        {
            Dictionary<Type, (Reference<int> leftPreLoad, HashSet<Type> postLoad, bool isFinalizer)> dependencyMap = new();

            foreach (var type in types) dependencyMap[type] = (new Reference<int>().Set(0), new(), type.GetCustomAttribute<NebulaPreLoad>()!.IsFinalizer);

            //有向グラフを作る
            foreach (var type in types)
            {
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

                foreach (var post in myAttr.PostLoadTypes)
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
                foreach (var post in dependencyMap[type].postLoad)
                {
                    if (dependencyMap.TryGetValue(post, out var postInfo))
                    {
                        postInfo.leftPreLoad.Update(v => v - 1);
                        if (postInfo.leftPreLoad.Value == 0) (postInfo.isFinalizer ? waitingFinalizerList : waitingList).Enqueue(post);
                    }
                }
            }

            //解決状況を出力
            var stringList = loadList.Join(t => "  -" + t.FullName, "\n");
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, "Dependencies resolved sequentially.\n" + stringList);

            if (loadList.Count < dependencyMap.Count)
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
                    catch (Exception e)
                    {
                        OnRaisedExcep(e, type);
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
                    if (coload != null) yield return coload.HandleException((e) => OnRaisedExcep(e, type));
                }
            }

            foreach (var type in loadList) yield return Preload(type);
        }
        FinishedPreload = true;
    }

    public static bool FinishedPreload { get; private set; } = false;
}
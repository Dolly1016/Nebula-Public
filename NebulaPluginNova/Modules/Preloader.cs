using Nebula.Behavior;
using Nebula.Roles;
using Nebula.Roles.Abilities;
using Nebula.Roles.Crewmate;
using Nebula.Roles.Impostor;
using System.Reflection;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;
using static Virial.Attributes.NebulaPreprocess;

namespace Nebula.Modules;

internal class NebulaPreprocessorImpl : NebulaPreprocessor
{
    static public bool Finished => Instance.FinishPreprocess;
    static internal NebulaPreprocessor Instance { get; private set; } = new NebulaPreprocessorImpl();

    DIManager NebulaPreprocessor.DIManager => DIManager.Instance;
    bool NebulaPreprocessor.FinishPreprocess => PreloadManager.FinishedPreload;

    private NebulaPreprocessorImpl()
    {
        NebulaAPI.preprocessor = this;
        preprocessList = new List<IEnumerator>[(int)PreprocessPhase.NumOfPhases];

        for (int i = 0; i < preprocessList.Length; i++) preprocessList[i] = [];
    }

    void NebulaPreprocessor.PickUpPreprocess(Assembly assembly)
    {
        foreach(var t in assembly.GetTypes())
        {
            var attr = t.GetCustomAttribute<NebulaPreprocess>();
            if (attr == null) continue;

            var method = t.GetMethod("Preprocess", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, [typeof(NebulaPreprocessor)]);
            if (method == null)
            {
                preprocessList[(int)attr.MyPhase].Add(((Action)(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle))).ToCoroutine());
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, t.Name + " doesn't have preprocessor. its static constructor is called instead.");
            }
            else if(method.ReturnType == typeof(void))
                preprocessList[(int)attr.MyPhase].Add(((Action)(() => method.Invoke(null, [this]))).ToCoroutine());
            else if (method.ReturnType == typeof(IEnumerator))
                preprocessList[(int)attr.MyPhase].Add((method.Invoke(null, [this]) as IEnumerator)!);
            else
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, t.Name + " has invalid preprocess that returns unsupported type.");
        }
    }

    void NebulaPreprocessor.RegisterAssignable(DefinedAssignable assignable)
    {
        if (assignable is DefinedRole dr)
            Roles.Roles.Register(dr);
        else if (assignable is DefinedModifier dm)
            Roles.Roles.Register(dm);
        else if (assignable is DefinedGhostRole dgr)
            Roles.Roles.Register(dgr);
        else
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, assignable.GetType().Name + " is unknown type.");
    }

    RoleTeam NebulaPreprocessor.CreateTeam(string translationKey, Virial.Color color, TeamRevealType revealType) => new Team(translationKey, color, revealType);

    void NebulaPreprocessor.SchedulePreprocess(PreprocessPhase phase, Action process) => (this as NebulaPreprocessor).SchedulePreprocess(phase, process.ToCoroutine());
    
    void NebulaPreprocessor.SchedulePreprocess(PreprocessPhase phase, IEnumerator process)
    {
        preprocessList[(int)phase].Add(process);
    }

    IEnumerator NebulaPreprocessor.RunPreprocess(Virial.Attributes.PreprocessPhase preprocess)
    {
        for(int i=0; i < preprocessList[(int)preprocess].Count; i++) {
            yield return preprocessList[(int)preprocess][i];
        }
    }

    IEnumerator NebulaPreprocessor.SetLoadingText(string text)
    {
        Patches.LoadPatch.LoadingText = text;
        yield break;
    }

    GameEnd NebulaPreprocessor.CreateEnd(string localizedName, Virial.Color color, int priority) => new(localizedName, color, priority);
    GameEnd NebulaPreprocessor.CreateEnd(string immutableId, TextComponent displayText, Virial.Color color, int priority) => new(immutableId, displayText, color, priority);
    ExtraWin NebulaPreprocessor.CreateExtraWin(string localizedName, Virial.Color color) => new(localizedName, color);
    ExtraWin NebulaPreprocessor.CreateExtraWin(string immutableId, TextComponent displayText, Virial.Color color) => new(immutableId, displayText, color);

    private List<IEnumerator>[] preprocessList;
}


[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public static class ToolsInstaller
{
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        if (NebulaPlugin.Log.IsPreferential)
        {
            Patches.LoadPatch.LoadingText = "Installing Tools";
            yield return null;

            InstallTool("CPUAffinityEditor.exe", null);

            InstallTool(Environment.Is64BitProcess ? "opus_x64.dll" : "opus_x86.dll", "opus.dll");
        }
    }

    private static void InstallTool(string name, string? outputName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream? stream = assembly.GetManifestResourceStream("Nebula.Resources.Tools." + name);
        if (stream == null) return;

        var file = File.Create(outputName ?? name);
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

    public static Exception? LastException = null;
    static private IEnumerator Preload()
    {
        DIManager.Instance.RegisterContainer(() => new NebulaGameManager());
        DIManager.Instance.RegisterContainer(() => new PlayerModInfo());
        DIManager.Instance.RegisterContainer(() => new GameModeFreePlayImpl());
        DIManager.Instance.RegisterContainer(() => new GameModeStandardImpl());

        //IModule<Virial.Game.Game>
        DIManager.Instance.RegisterModule(() => new Synchronizer());
        DIManager.Instance.RegisterModule(() => new MeetingPlayerButtonManager());
        DIManager.Instance.RegisterModule(() => new MeetingOverlayHolder());
        DIManager.Instance.RegisterModule(() => new TitleShower());
        DIManager.Instance.RegisterModule(() => new PerkHolder());
        DIManager.Instance.RegisterModule(() => new FakeInformation());

        //IModule<Virial.Game.Player>
        DIManager.Instance.RegisterModule(() => new PlayerTaskState());
        



        //NoSのプリプロセッサを取得
        NebulaPreprocessorImpl.Instance.PickUpPreprocess(typeof(PreloadManager).Assembly);

        static void OnRaisedExcep(Exception exception)
        {
            LastException ??= exception;
        }

        yield return NebulaPreprocessorImpl.Instance.SetLoadingText("Checking Component Dependencies");

        for(int i=0;i<(int)PreprocessPhase.NumOfPhases;i++) yield return NebulaPreprocessorImpl.Instance.RunPreprocess((PreprocessPhase)i).HandleException(OnRaisedExcep);

        yield return NebulaPreprocessorImpl.Instance.SetLoadingText("Finalizing...");

        GC.Collect(2);

        FinishedPreload = true;
    }

    public static bool FinishedPreload { get; private set; } = false;
}
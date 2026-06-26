using Nebula.Behavior;
using Nebula.Dev;
using Nebula.Modules.Logging;
using Nebula.Roles;
using Nebula.Roles.Abilities;
using Nebula.Roles.Crewmate;
using Nebula.Roles.Impostor;
using Nebula.SpecialModes.AeroGuesser;
using Nebula.SpecialModes.PaintQuiz;
using System.Reflection;
using Virial;
using Virial.Achievements;
using Virial.Assignable;
using Virial.DI;
using Virial.Game;
using Virial.Game.Console;
using Virial.Runtime;
using Virial.Text;
using Virial.Utilities;
using static Virial.Attributes.NebulaPreprocess;

namespace Nebula.Modules;

internal class NebulaPreprocessorImpl : NebulaPreprocessor
{
    static public bool Finished => Instance.FinishPreprocess;
    private PreprocessPhase currentPhase = PreprocessPhase.BuildNoSModuleContainer;
    static internal NebulaPreprocessor Instance { get; private set; } = new NebulaPreprocessorImpl();

    DIManager NebulaPreprocessor.DIManager => DIManager.Instance;
    bool NebulaPreprocessor.FinishPreprocess => PreloadManager.FinishedPreload;

    private NebulaPreprocessorImpl()
    {
        NebulaAPI.preprocessor = this;
        preprocessList = new List<Func<IEnumerator>>[(int)PreprocessPhase.NumOfPhases];

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
                preprocessList[(int)attr.MyPhase].Add(() => ManagedEffects.Action(() => Helpers.RunStaticConstructor(t)));
                NebulaLogger.Instance.Message(t.Name + " doesn't have preprocessor. its static constructor is called instead.");
            }
            else if(method.ReturnType == typeof(void))
                preprocessList[(int)attr.MyPhase].Add(() => ManagedEffects.Action(()=> method.Invoke(null, [this])));
            else if (method.ReturnType == typeof(IEnumerator))
                preprocessList[(int)attr.MyPhase].Add(() => (method.Invoke(null, [this]) as IEnumerator)!);
            else
                NebulaLogger.Instance.Error(t.Name + " has invalid preprocess that returns unsupported type.");
        }
    }

    CommunicableTextTag NebulaPreprocessor.RegisterCommunicableText(string translationKey) => new TranslatableTag(translationKey);

    void NebulaPreprocessor.RegisterAssignable(DefinedAssignable assignable)
    {
        if (assignable is DefinedRole dr)
            Roles.Roles.Register(dr);
        else if (assignable is DefinedModifier dm)
            Roles.Roles.Register(dm);
        else if (assignable is DefinedGhostRole dgr)
            Roles.Roles.Register(dgr);
        else
            NebulaLogger.Instance.Error(assignable.GetType().Name + " is unknown type.");
    }

    AssignmentType NebulaPreprocessor.RegisterAssignmentType(Func<DefinedRole> relatedRole, Func<int[], DefinedRole, int[]> argumentEditor, string postfix, Virial.Color? color, Func<AbilityAssignmentStatus, DefinedRole, bool> predicate, Func<bool> isActive, bool canGuessAsAbility)
    {
        if (currentPhase >= PreprocessPhase.PreRoles) return null!;
        return new AssignmentType(relatedRole, argumentEditor, postfix, color, predicate, isActive, canGuessAsAbility);
    }

    RoleTeam NebulaPreprocessor.CreateTeam(string translationKey, Virial.Color color, TeamRevealType revealType) => new Team(translationKey, color, revealType);

    bool NebulaPreprocessor.RegisterRpcType<T, V>(Func<T?, V> serializer, Func<V, T?> deserializer) where T : class
    {
        var process = RemoteProcessAsset.GetProcess(typeof(V));
        new RemoteProcessArgument<T>((writer, val) => {
            process.Item1.Invoke(writer, serializer.Invoke(val));
        }, (reader) => { 
            return (T?)process.Item2.Invoke(reader)!;
        });
        return true;
    }

    void NebulaPreprocessor.SchedulePreprocess(PreprocessPhase phase, Action process) => (this as NebulaPreprocessor).SchedulePreprocess(phase, process.ToCoroutine());
    
    void NebulaPreprocessor.SchedulePreprocess(PreprocessPhase phase, IEnumerator process)
    {
        preprocessList[(int)phase].Add(() => process);
    }

    IEnumerator NebulaPreprocessor.RunPreprocess(Virial.Attributes.PreprocessPhase preprocess)
    {
        for(int i=0; i < preprocessList[(int)preprocess].Count; i++) {
            currentPhase = (PreprocessPhase)i;
            yield return preprocessList[(int)preprocess][i].Invoke();
        }
    }

    IEnumerator NebulaPreprocessor.SetLoadingText(string text)
    {
        Patches.LoadPatch.LoadingText = text;
        yield break;
    }

    GameEnd NebulaPreprocessor.CreateEnd(string localizedName, Virial.Color color, int priority, bool specifyNobodyWins) => new(localizedName, color, priority, specifyNobodyWins);
    GameEnd NebulaPreprocessor.CreateEnd(string immutableId, TextComponent displayText, Virial.Color color, int priority, bool specifyNobodyWins) => new(immutableId, displayText, color, priority, specifyNobodyWins);
    ExtraWin NebulaPreprocessor.CreateExtraWin(string localizedName, Virial.Color color) => new(localizedName, color);
    ExtraWin NebulaPreprocessor.CreateExtraWin(string immutableId, TextComponent displayText, Virial.Color color) => new(immutableId, displayText, color);
    UseButtonAlternative NebulaPreprocessor.RegisterUseButtonAlternative(Virial.Media.Image buttonImage, Action onUsed) => new(buttonImage, () => true, _ => onUsed.Invoke(), false);

    private List<Func<IEnumerator>>[] preprocessList;

    ITitlesRegister NebulaPreprocessor.Titles => TitleRegisterImpl.Instance;
}


[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public static class ToolsInstaller
{
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
#if PC
        if (NebulaPlugin.IsPreferential)
        {
            Patches.LoadPatch.LoadingText = "Installing Tools";
            yield return null;

            InstallTool("CPUAffinityEditor.exe", null, "Tools");
            InstallTool("AddonScriptCompiler.dll", null, "Tools");
            InstallTool("AddonScriptCompiler.exe", null, "Tools");
            InstallTool("AddonScriptCompiler.runtimeconfig.json", null, "Tools");
            InstallTool("AddonScriptCompiler.deps.json", null, "Tools");
            InstallTool(Environment.Is64BitProcess ? "opus_x64.dll" : "opus_x86.dll", "opus.dll");
        }
#else
        yield break;
#endif
    }

    private static void InstallTool(string name, string? outputName, string? directory = null)
    {
        if (directory != null) Directory.CreateDirectory(directory);

        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream("Nebula.Resources.Tools." + name);
        if (stream == null) return;

        var filePath = outputName ?? name;
        if (directory != null) filePath = directory + Path.DirectorySeparatorChar + filePath;
        using var file = File.Create(filePath);
        byte[] data = new byte[stream.Length];
        stream.Read(data);
        file.Write(data);
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
        DIManager.Instance.RegisterContainer(() => new AeroGuesserSenario());
        DIManager.Instance.RegisterContainer(() => new PaintQuizSenario());

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
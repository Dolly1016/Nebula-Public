using Cpp2IL.Core.Extensions;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Virial;
using Virial.Runtime;

namespace Nebula.Scripts;

internal class AddonBehaviour
{
    [JsonSerializableField]
    public bool LoadRoles = false;
    [JsonSerializableField]
    public bool UseHiddenMembers = false;
    [JsonSerializableField]
    public List<string> References = [];
}

internal record AddonScript(Assembly Assembly, NebulaAddon Addon, object? Reference, AddonBehaviour Behaviour);

internal static class LibraryLoader
{
    private static ZipArchive? archive = null;
    static public byte[]? OpenLibrary(string libraryName)
    {
        if (archive == null)
        {
            var libs = StreamHelper.OpenFromResource("Nebula.Resources.Libs.zip");
            archive = new ZipArchive(libs!, ZipArchiveMode.Read);
        }
        return archive.GetEntry("Libs/" + libraryName)?.Open().ReadBytes();
    }
    static public void CloseZip()
    {
        archive?.Dispose();
        archive = null;
    }
}

[NebulaPreprocess(PreprocessPhase.CompileAddons)]
internal static class AddonScriptManagerLoader
{
    private static bool setUpDone = false;
    static internal object[] ReferenceAssemblies = [];
    internal static void SetUp()
    {
        if (setUpDone) return;
        setUpDone = true;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        NebulaPlugin.NoSAssemblyContext.LoadFromStream(new MemoryStream(LibraryLoader.OpenLibrary("System.Collections.Immutable.dll")!));
        NebulaPlugin.NoSAssemblyContext.LoadFromStream(new MemoryStream(LibraryLoader.OpenLibrary("System.Reflection.Metadata.dll")!));
        NebulaPlugin.NoSAssemblyContext.LoadFromStream(new MemoryStream(LibraryLoader.OpenLibrary("Microsoft.CodeAnalysis.dll")!));
        NebulaPlugin.NoSAssemblyContext.LoadFromStream(new MemoryStream(LibraryLoader.OpenLibrary("Microsoft.CodeAnalysis.CSharp.dll")!));
        var loader = NebulaPlugin.NoSAssemblyContext.LoadFromStream(new MemoryStream(LibraryLoader.OpenLibrary("AddonScriptLoader.dll")!));
        LibraryLoader.CloseZip();
        var type = loader.GetType("AddonScriptLoader.ScriptCompiler");
        CompileMethod = type?.GetMethod("CompileScripts", BindingFlags.Static | BindingFlags.Public)!;
        SearchAssembliesMethod = type?.GetMethod("SearchReferences", BindingFlags.Static | BindingFlags.Public)!;
        PreprocessMethod = type?.GetMethod("PreprocessSource", BindingFlags.Static | BindingFlags.Public)!;
#if PC
        using var apiStream = StreamHelper.OpenFromResource("Nebula.Resources.API.NebulaAPI.dll")!;
        ReferenceAssemblies = (object[])AddonScriptManagerLoader.SearchAssembliesMethod.Invoke(null, [assemblies, apiStream.ReadBytes()])!;
#else
        ReferenceAssemblies = (object[])AddonScriptManagerLoader.SearchAssembliesMethod.Invoke(null, [assemblies, null])!;
#endif
    }
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        Patches.LoadPatch.LoadingText = "Compiling Addon Scripts";
        yield return null;
        yield return AddonScriptManager.CoLoad(preprocessor);
    }
    static internal MethodInfo CompileMethod { get; private set; } = null!;
    static internal MethodInfo SearchAssembliesMethod { get; private set; } = null!;
    static internal MethodInfo PreprocessMethod { get; private set; } = null!;
}

internal static class AddonScriptManager
{
    static public IEnumerable<AddonScript> ScriptAssemblies => scriptAssemblies;
    static private List<AddonScript> scriptAssemblies = [];
    static private Virial.Logging.ILogger Log;
    static private HashSet<string> loadedAddonIds = [];
    static private void PrintToLog(int severity, string path, int line, int character, string id, string message)
    {
        string labeledMessage = $"{id}: {message} at {path} (Line: {line + 1}, Character: {character + 1})";
        switch (severity)
        {
            case 0:
            case 1:
                Log.Message(labeledMessage);
                break;
            case 2:
                Log.Warning(labeledMessage);
                break;
            default:
                Log.Error(labeledMessage);
                break;
        }
    }

    static public IEnumerator CoLoad(NebulaPreprocessor preprocessor)
    {
        Log = NebulaAPI.Logging.NebulaLogger("Scripting");
        loadedAddonIds = new HashSet<string>(NebulaAddon.AllAddons.Select(a => a.Id));

        var sourceUnits = new List<SourceUnit>();
        foreach (var addon in NebulaAddon.AllAddons)
        {
            string prefix = addon.InZipPath + "Scripts/";
            AddonBehaviour? behaviour = null;
            var behaviourEntry = addon.Archive.GetEntry(prefix + ".behaviour");
            if (behaviourEntry != null)
            {
                using var stream = behaviourEntry.Open();
                behaviour = JsonStructure.Deserialize<AddonBehaviour>(stream);
            }
            behaviour ??= new AddonBehaviour();

            var dllFiles = addon.Archive.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".dll")).ToArray();
            if (dllFiles.Length > 0)
            {
                yield return preprocessor.SetLoadingText("Loading Addon Scripts\n" + addon.Id);
                foreach (var dll in dllFiles)
                {
                    using var stream = dll.Open();
                    var assembly = NebulaPlugin.NoSAssemblyContext.LoadFromStream(stream);
                    scriptAssemblies.Add(new(assembly, addon, null, behaviour));
                    NebulaAPI.Preprocessor?.PickUpPreprocess(assembly);
                }
                continue;
            }

            var csFiles = addon.Archive.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".cs")).ToArray();
            if (csFiles.Length == 0) continue;

            AddonScriptManagerLoader.SetUp();
            var sources = new List<(string source, string path)>();
            foreach (var cs in csFiles)
            {
                string raw = cs.Open().ReadToEnd();
                string pathRel = cs.FullName.Substring(prefix.Length);
                string preprocessed = (string)AddonScriptManagerLoader.PreprocessMethod.Invoke(null, [raw, loadedAddonIds])!;
                sources.Add((preprocessed, pathRel));
            }
            sourceUnits.Add(new SourceUnit(addon, behaviour, sources));
        }

        var compiled = new Dictionary<string, AddonScript>();
        var pending = new Queue<SourceUnit>(sourceUnits);
        int lastCount = -1;
        while (pending.Count > 0 && pending.Count != lastCount)
        {
            lastCount = pending.Count;
            var current = pending.Dequeue();
            var references = new List<object>(AddonScriptManagerLoader.ReferenceAssemblies);
            foreach (var refId in current.Behaviour.References)
            {
                if (compiled.TryGetValue(refId, out var refScript))
                    references.Add(refScript.Reference!);
                else if (NebulaAddon.GetAddon(refId) != null)
                {
                    pending.Enqueue(current);
                    goto nextIteration;
                }
            }
            yield return preprocessor.SetLoadingText("Compiling Addon Scripts\n" + current.Addon.Id);
            var assemblyTuple = (Tuple<Assembly, object>?)AddonScriptManagerLoader.CompileMethod.Invoke(null,
            [
                "Script." + current.Addon.Id.HeadUpper(),
                NebulaPlugin.NoSAssemblyContext,
                references.ToArray(),
                (Action<int, string, int, int, string, string>)PrintToLog,
                current.Sources,
                current.Behaviour.UseHiddenMembers
            ]);
            if (assemblyTuple == null)
            {
                Log.Error($"Compile Error! Scripts ignored (Addon: {current.Addon.Id})");
            }
            else
            {
                var script = new AddonScript(assemblyTuple.Item1, current.Addon, assemblyTuple.Item2, current.Behaviour);
                scriptAssemblies.Add(script);
                compiled[current.Addon.Id] = script;
                NebulaAPI.Preprocessor?.PickUpPreprocess(assemblyTuple.Item1);
            }
        nextIteration:;
        }
        if (pending.Count > 0)
        {
            foreach (var unit in pending)
                Log.Error($"Circular or missing reference detected. Addon '{unit.Addon.Id}' could not be compiled.");
        }
        yield break;
    }

    private class SourceUnit
    {
        public NebulaAddon Addon;
        public AddonBehaviour Behaviour;
        public List<(string source, string path)> Sources;
        public SourceUnit(NebulaAddon a, AddonBehaviour b, List<(string, string)> s)
        {
            Addon = a;
            Behaviour = b;
            Sources = s;
        }
    }
}

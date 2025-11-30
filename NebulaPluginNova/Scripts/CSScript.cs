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
}

internal record AddonScript(Assembly Assembly, NebulaAddon Addon, object Reference, AddonBehaviour Behaviour);

internal static class LibraryLoader
{
    private static ZipArchive? archive = null;
    static public byte[]? OpenLibrary(string libraryName)
    {
        if(archive == null)
        {
            var libs = StreamHelper.OpenFromResource("Nebula.Resources.Libs.zip");
            archive = new ZipArchive(libs!, ZipArchiveMode.Read);
            archive.Entries.Do(e => Debug.Log(e.FullName));
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
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        Patches.LoadPatch.LoadingText = "Compiling Addon Scripts";
        yield return null;

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

        /*
        Assembly.Load(LibraryLoader.OpenLibrary("System.Collections.Immutable.dll")!);
        Assembly.Load(LibraryLoader.OpenLibrary("System.Reflection.Metadata.dll")!);
        Assembly.Load(LibraryLoader.OpenLibrary("Microsoft.CodeAnalysis.dll")!);
        Assembly.Load(LibraryLoader.OpenLibrary("Microsoft.CodeAnalysis.CSharp.dll")!);
        */

        yield return AddonScriptManager.CoLoad(preprocessor, assemblies);
    }

    static internal MethodInfo CompileMethod { get; private set; } = null!;
    static internal MethodInfo SearchAssembliesMethod { get; private set; } = null!;
}

internal static class AddonScriptManager
{
    static private object[] ReferenceAssemblies = [];
    static public IEnumerable<AddonScript> ScriptAssemblies => scriptAssemblies;
    static private List<AddonScript> scriptAssemblies = [];
    static private NebulaLog.LogLevel[] logLevels = [NebulaLog.LogLevel.Log, NebulaLog.LogLevel.Log, NebulaLog.LogLevel.Warning, NebulaLog.LogLevel.Error];
    static private void PrintToLog(int severity, string path, int line, int character, string id, string message) {
        NebulaPlugin.Log.Print(logLevels[severity], NebulaLog.LogCategory.Scripting, $"{id}: {message} at {path} (Line: {line + 1}, Character: {character + 1})");
    }
    static public IEnumerator CoLoad(NebulaPreprocessor preprocessor, Assembly[] assemblies)
    {
#if PC
        using var apiStream = StreamHelper.OpenFromResource("Nebula.Resources.API.NebulaAPI.dll")!;
        ReferenceAssemblies = (object[])AddonScriptManagerLoader.SearchAssembliesMethod.Invoke(null, [assemblies, apiStream.ReadBytes()])!;
#else
        ReferenceAssemblies = (object[])AddonScriptManagerLoader.SearchAssembliesMethod.Invoke(null, [assemblies, null])!;
#endif

        foreach (var addon in NebulaAddon.AllAddons)
        {
            string prefix = addon.InZipPath + "Scripts/";

            var sources = addon.Archive.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".cs")).Select(e => (e.Open().ReadToEnd(), e.FullName.Substring(prefix.Length))).ToArray();
            if (sources.Length == 0) continue;

            yield return preprocessor.SetLoadingText("Compiling Addon Scripts\n" + addon.Id);

            AddonBehaviour? addonBehaviour = null;
            var behaviour = addon.Archive.GetEntry(prefix + ".behaviour");
            if (behaviour != null)
            {
                using var stream = behaviour.Open();
                addonBehaviour = JsonStructure.Deserialize<AddonBehaviour>(stream);
            }

            var assembly = (Tuple<Assembly, object>?)AddonScriptManagerLoader.CompileMethod.Invoke(null, ["Script." + addon.Id.HeadUpper(), NebulaPlugin.NoSAssemblyContext, ReferenceAssemblies, (Action<int, string, int, int, string, string>)PrintToLog, sources, addonBehaviour?.UseHiddenMembers ?? false]);

            if(assembly == null) { 
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Scripting, "Compile Error! Scripts is ignored (Addon: " + addon.Id + ")");
            }
            else
            {
                scriptAssemblies.Add(new(assembly.Item1, addon, assembly.Item2, addonBehaviour ?? new()));
                NebulaAPI.Preprocessor?.PickUpPreprocess(assembly.Item1);
            }
        }

        /*
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings("Virial", "Virial.Compat", "System", "System.Linq", "System.Collections.Generic")
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithMetadataImportOptions(MetadataImportOptions.All);

        foreach (var addon in NebulaAddon.AllAddons)
        {
            string prefix = addon.InZipPath + "Scripts/";
            
            AddonBehaviour? addonBehaviour = null;
            var behaviour = addon.Archive.GetEntry(prefix + ".behaviour");
            if (behaviour != null)
            {
                using var stream = behaviour.Open();
                addonBehaviour = JsonStructure.Deserialize<AddonBehaviour>(stream);
            }

            List<SyntaxTree> trees = [];
            foreach(var entry in addon.Archive.Entries)
            {
                if (!entry.FullName.StartsWith(prefix)) continue;

                if (entry.FullName.EndsWith(".cs"))
                {
                    //解析木をつくる
                    trees.Add(CSharpSyntaxTree.ParseText(entry.Open().ReadToEnd(), parseOptions, entry.FullName.Substring(prefix.Length), Encoding.UTF8));
                }
            }
            
            //解析木が一つも無ければコンパイルは不要
            if (trees.Count == 0) continue;

            Patches.LoadPatch.LoadingText = "Compiling Addon Scripts\n" + addon.Id;
            yield return null;

            var myCompilationOptions = compilationOptions.WithModuleName("Script." + addon.Id.HeadUpper());

            if (addonBehaviour?.UseHiddenMembers ?? false)
            {
                //全Internal, Privateメンバにアクセスできるようにする
                var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic)!;
                topLevelBinderFlagsProperty.SetValue(myCompilationOptions, (uint)1 << 22);

                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"Nebula\")]\n[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"NebulaAPI\")]", parseOptions, "", Encoding.UTF8));
            }

            var compilation = CSharpCompilation.Create("Script." + addon.Id.HeadUpper(), trees, ReferenceAssemblies, myCompilationOptions)
                .AddReferences(scriptAssemblies.Where(a => addon.Dependency.Contains(a.Addon)).Select(a => a.Reference));
            
            Assembly? assembly = null;
            using (var stream = new MemoryStream())
            {
                var emitResult = compilation.Emit(stream);

                if (emitResult.Diagnostics.Length > 0) {
                    string log = "Compile Log:";
                    foreach (var diagnostic in emitResult.Diagnostics)
                    {
                        var pos = diagnostic.Location.GetLineSpan();
                        var location = "(" + pos.Path + " at line " + (pos.StartLinePosition.Line + 1) + ", character" + (pos.StartLinePosition.Character + 1) + ")";

                        log += $"\n[{diagnostic.Severity}, {location}] {diagnostic.Id}, {diagnostic.GetMessage()}";
                    }
                    NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.Scripting, log);
                    
                }

                if (emitResult.Success)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    assembly = NebulaPlugin.NoSAssemblyContext.LoadFromStream(stream); //もとはデフォルトのコンテキストでロードしていた。
                }
                else
                {
                    NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Scripting, "Compile Error! Scripts is ignored (Addon: " + addon.Id + ")");
                }
            }

            if (assembly != null)
            {
                scriptAssemblies.Add(new(assembly, addon, compilation.ToMetadataReference(), addonBehaviour ?? new()));
                NebulaAPI.Preprocessor?.PickUpPreprocess(assembly);
            }
        }
        */

        yield break;
    }

}
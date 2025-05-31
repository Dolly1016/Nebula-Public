using Cpp2IL.Core.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
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

internal record AddonScript(Assembly Assembly, NebulaAddon Addon, MetadataReference Reference, AddonBehaviour Behaviour);

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
}

[NebulaPreprocess(PreprocessPhase.CompileAddons)]
internal static class AddonScriptManagerLoader
{
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        Patches.LoadPatch.LoadingText = "Compiling Addon Scripts";
        yield return null;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        Assembly.Load(LibraryLoader.OpenLibrary("System.Collections.Immutable.dll")!);
        Assembly.Load(LibraryLoader.OpenLibrary("System.Reflection.Metadata.dll")!);
        Assembly.Load(LibraryLoader.OpenLibrary("Microsoft.CodeAnalysis.dll")!);
        Assembly.Load(LibraryLoader.OpenLibrary("Microsoft.CodeAnalysis.CSharp.dll")!);
        
        yield return AddonScriptManager.CoLoad(assemblies);
    }
}


internal static class AddonScriptManager
{
    static private MetadataReference[] ReferenceAssemblies = [];
    static public IEnumerable<AddonScript> ScriptAssemblies => scriptAssemblies;
    static private List<AddonScript> scriptAssemblies = [];
    static public IEnumerator CoLoad(Assembly[] assemblies)
    {

        //参照可能なアセンブリを抽出する
        ReferenceAssemblies = assemblies.Where(a => { try { return ((a.Location?.Length ?? 0) > 0); } catch { return false; } }).Select(a => MetadataReference.CreateFromFile(a.Location)).Append(MetadataReference.CreateFromImage(StreamHelper.OpenFromResource("Nebula.Resources.API.NebulaAPI.dll")!.ReadBytes())).ToArray();

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
                    assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
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

        yield break;
    }
}

/*

namespace Nebula.Scripts
{

    public class ScriptInteraction : InteractiveBase { }
    public class CSScripting
    {
        static readonly HashSet<string> Assemblies =
        new HashSet<string>(){
            "mscorlib",
            "netstandard",
            
            "System.Runtime",
            "System.Runtime.Numerics",
            "System.Runtime.Loader",
            //"System.Runtime.InteropServices",
            //"System.Runtime.InteropServices.RuntimeInformation",
            
            //"System.Threading",
            //"System.Threading.ThreadPool",
            //"System.Threading.Overlapped",

            "System.Memory",
            "System.Collections",
            "System.Collections.NonGeneric",
            "System.Collections.Specialized",
            //"System.Collections.Concurrent",
            
            "System.Linq",
            "System.Linq.Expressions",

            "System.Text.RegularExpressions",
            "System.Text.Encoding.Extensions",
            "System.Text.Json",

            "System.Console",

            "System.Diagnostics.StackTrace",
            "System.Diagnostics.TraceSource",
            //"System.Diagnostics.FileVersionInfo",

            //"System.IO.FileSystem",
            
            //"System.Reflection.Primitives",
            
            "System",
            "System.Core",
            //"System.Xml",
            
            "System.Private.CoreLib",

            "NebulaAPI"
        };

        Evaluator evaluator;
        StringBuilder myOutput;
        ReportPrinter reportPrinter;
        NebulaAddon addon;
        public NebulaAddon Addon { get => addon; }

        public CSScripting(NebulaAddon addon)
        {
            myOutput = new StringBuilder();

            CompilerSettings settings = new()
            {
                Version = LanguageVersion.V_7_2,
                GenerateDebugInfo = false,
                StdLib = true,
                Target = Target.Library,
                WarningLevel = 0,
                EnhancedWarnings = false
            };

            reportPrinter = new StreamReportPrinter(new StringWriter(myOutput));
            evaluator = new Evaluator(new CompilerContext(settings, reportPrinter)) { InteractiveBaseClass = typeof(ScriptInteraction) };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {

                string? name = assembly.GetName().Name;
                if (name != null && Assemblies.Contains(name)) evaluator.ReferenceAssembly(assembly);
            }

            this.addon = addon; 
        }

        
        public bool Evaluate(string program, out string? errorText) => Evaluate(program, out errorText, out _);
        static private FieldInfo? sourceFileField = null;
        static private FieldInfo SourceFileField
        {
            get
            {
                sourceFileField ??= typeof(Evaluator).GetField("source_file", BindingFlags.Instance | BindingFlags.NonPublic);
                return sourceFileField!;
            }
        }

        static private FieldInfo? moduleField = null;
        static private FieldInfo ModuleField
        {
            get
            {
                moduleField ??= typeof(Evaluator).GetField("module", BindingFlags.Instance | BindingFlags.NonPublic)!;
                return moduleField;
            }
        }

        private Assembly myAssembly;
        public Assembly Assembly { get
            {
                myAssembly ??= (ModuleField.GetValue(evaluator) as ModuleContainer)!.DeclaringAssembly.Builder;
                return myAssembly;
            } 
        }

        public bool Evaluate(string program, out string? errorText,out object? result)
        {
            (SourceFileField.GetValue(evaluator) as CompilationSourceFile)?.Usings?.Clear();

            result = null;
            errorText = null;

            CompiledMethod repl = evaluator.Compile(program);

            if (repl != null)
            {
                try
                {
                    object ret = null!;
                    repl.Invoke(ref ret);
                    result = ret;
                    Debug.Log(ret?.ToString() ?? "-");
                }
                catch (Exception ex)
                {
                    errorText = ex.ToString();
                }
            }
            else
            {
                if (reportPrinter.ErrorsCount > 0) errorText = "Compile Error";
                reportPrinter.Reset();
            }
            return errorText == null;
        }
        public string PopLogText()
        {
            var result = myOutput.ToString();
            myOutput.Clear();
            return result;
        }
    }
}


public static class AddonScriptManager
{
    static Dictionary<NebulaAddon, CSScripting> scriptings = new();

    static public bool TryGetScripting(NebulaAddon addon, [MaybeNullWhen(false)]  out CSScripting script)
    {
        return scriptings.TryGetValue(addon, out script);
    }

    static public CSScripting GetScripting(NebulaAddon addon)
    {
        if (!scriptings.TryGetValue(addon, out var scripting))
        {
            scripting = new CSScripting(addon);
            scripting.Evaluate($"#define NOS_API_{Virial.NebulaAPI.APIVersion.Replace('.', '_')}\n", out _);
            scriptings.Add(addon, scripting);
        }
        return scripting;
    }

    static public CSScripting? GetScriptingByAssembly(Assembly assembly)
    {
        return scriptings.Values.FirstOrDefault(script => script.Assembly == assembly);
    }

    static private void Evaluate(CSScripting scripting, ZipArchiveEntry program)
    {
        using var reader = new StreamReader(program.Open());
        if (!scripting.Evaluate(reader.ReadToEnd(), out var error))
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Error has occurred in " + program.Name + "\n" + error + "\n" + scripting.PopLogText());
    }

    public static void EvaluateScript(string phase)
    {
        foreach (var addon in NebulaAddon.AllAddons)
        {
            string predicatePath = addon.InZipPath + "Scripts/" + phase + "/";
            CSScripting? scripting = null;

            foreach (var entry in addon.Archive.Entries)
            {
                if (!entry.FullName.StartsWith(predicatePath)) continue;

                scripting ??= GetScripting(addon);
                Evaluate(scripting, entry);
            }
        }
    }

    public static void ExecuteEvent(CallingEvent callingEvent) {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.GetName().Name?.StartsWith("eval") ?? false) continue;

            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.IsStatic && method.GetParameters().Length == 0 && (((int?)method.GetCustomAttribute<CallingRuleAttribute>()?.MyEventFlag ?? 0) & (int)callingEvent) != 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }
    }
}
*/
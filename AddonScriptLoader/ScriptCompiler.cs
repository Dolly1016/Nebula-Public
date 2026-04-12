using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

namespace AddonScriptLoader;

static public class ScriptCompiler
{
    static private CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
    static private CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings("Virial", "Virial.Compat", "System", "System.Linq", "System.Collections.Generic")
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithMetadataImportOptions(MetadataImportOptions.All);

    static public object[] SearchReferences(Assembly[] assemblies, byte[]? additionalLibrary)
    {
        var result = assemblies.Where(a => { try { return ((a.Location?.Length ?? 0) > 0); } catch { return false; } }).Select(a => MetadataReference.CreateFromFile(a.Location));
        if (additionalLibrary != null) result = result.Append(MetadataReference.CreateFromImage(additionalLibrary));
        return result.ToArray();
    }

    static public string PreprocessSource(string source, HashSet<string> definedSymbols)
    {
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var output = new StringBuilder();
        var stack = new Stack<CondState>();
        stack.Push(new CondState { Active = true, Taken = false });

        var ifRegex = new Regex(@"^\s*#if\s+(\S+)\s*$");
        var elifRegex = new Regex(@"^\s*#elif\s+(\S+)\s*$");
        var elseRegex = new Regex(@"^\s*#else\s*$");
        var endifRegex = new Regex(@"^\s*#endif\s*$");

        foreach (var line in lines)
        {
            Match m;
            if ((m = ifRegex.Match(line)).Success)
            {
                var top = stack.Peek();
                bool cond = definedSymbols.Contains(m.Groups[1].Value);
                bool active = top.Active && cond && !top.Taken;
                if (active) top.Taken = true;
                stack.Push(new CondState { Active = active, Taken = false });
            }
            else if ((m = elifRegex.Match(line)).Success)
            {
                var top = stack.Pop();
                bool cond = definedSymbols.Contains(m.Groups[1].Value);
                bool active = top.Active && !top.Taken && cond;
                if (active) top.Taken = true;
                stack.Push(new CondState { Active = active, Taken = false });
            }
            else if (elseRegex.IsMatch(line))
            {
                var top = stack.Pop();
                bool active = top.Active && !top.Taken;
                if (active) top.Taken = true;
                stack.Push(new CondState { Active = active, Taken = false });
            }
            else if (endifRegex.IsMatch(line))
            {
                stack.Pop();
            }
            else if (stack.Peek().Active)
            {
                output.AppendLine(line);
            }
        }
        return output.ToString();
    }

    private class CondState
    {
        public bool Active;
        public bool Taken;
    }

    static public Tuple<Assembly, object?>? CompileScripts(string moduleName, AssemblyLoadContext context, object[] reference, Action<int, string, int, int, string, string> logger, IEnumerable<(string source, string path)> sources, bool useHiddenMembers)
    {
        List<SyntaxTree> trees = [];
        foreach (var tuple in sources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(tuple.source, ParseOptions, tuple.path, Encoding.UTF8));
        }
        if (trees.Count == 0) return null;
        var myCompilationOptions = CompilationOptions.WithModuleName(moduleName);
        if (useHiddenMembers)
        {
            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic)!;
            topLevelBinderFlagsProperty.SetValue(myCompilationOptions, (uint)1 << 22);
            trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"Nebula\")]\n[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"NebulaAPI\")]", ParseOptions, "", Encoding.UTF8));
        }
        var compilation = CSharpCompilation.Create(moduleName, trees, reference.Select(r => (MetadataReference)r), myCompilationOptions);
        Assembly? assembly = null;
        using (var stream = new MemoryStream())
        {
            var emitResult = compilation.Emit(stream);
            if (emitResult.Diagnostics.Length > 0)
            {
                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    var pos = diagnostic.Location.GetLineSpan();
                    logger.Invoke((int)diagnostic.Severity, pos.Path, pos.StartLinePosition.Line, pos.StartLinePosition.Character, diagnostic.Id, diagnostic.GetMessage());
                }
            }
            if (emitResult.Success)
            {
                stream.Seek(0, SeekOrigin.Begin);
                assembly = context.LoadFromStream(stream);
                return new(assembly, compilation.ToMetadataReference());
            }
        }
        return null;
    }
}

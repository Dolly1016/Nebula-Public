using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace NebulaAndroid.CSScriptAndroid;

internal static class AndroidCompilerSupport
{
    internal static void AddDependencyResolver()
    {
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            // 依存関係にあるアセンブリ名を判定
            // 提示された2つのライブラリを対象にする
            if (assemblyName.Name == "System.Reflection.Metadata" ||
                assemblyName.Name == "System.Collections.Immutable")
            {
                // DLLのフルパスを組み立てる
                string dllPath = Path.Combine(PathHelpers.NebulaLibsPath, $"{assemblyName.Name}.dll");

                if (File.Exists(dllPath))
                {
                    return context.LoadFromAssemblyPath(dllPath);
                }
            }

            return null; // 他のアセンブリは通常通りの解決に任せる
        };
    }
}

internal static class Compiler
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);

    private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary) 
            .WithUsings("Virial", "Virial.Compat", "System", "System.Linq", "System.Collections.Generic")
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithMetadataImportOptions(MetadataImportOptions.All);

    internal static bool Compile(
        string moduleName,
        string[] references,
        (string fileName, string source)[] sources,
        string outputPath,
        bool useHiddenMembers)
    {
        List<SyntaxTree> trees = new();

        var myParseOptions = ParseOptions.WithPreprocessorSymbols("ANDROID");
        foreach (var source in sources) trees.Add(CSharpSyntaxTree.ParseText(source.source, myParseOptions, source.fileName, Encoding.UTF8));
        
        if (trees.Count == 0) return false;
        
        var myCompilationOptions = CompilationOptions.WithModuleName(moduleName);

        if (useHiddenMembers)
        {
            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (topLevelBinderFlagsProperty != null)
            {
                topLevelBinderFlagsProperty.SetValue(myCompilationOptions, (uint)1 << 22);
            }
            trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"NebulaAndroid\")]", ParseOptions, "", Encoding.UTF8));
        }

        var metadataReferences = references
            .Where(r => !string.IsNullOrWhiteSpace(r) && File.Exists(r))
            .Select(r => MetadataReference.CreateFromFile(r))
            .ToList();

        var compilation = CSharpCompilation.Create(moduleName, trees, metadataReferences, myCompilationOptions);

        using var dllStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        var emitResult = compilation.Emit(dllStream);
        List<string> logs = [$"Compile Log (Addon: {moduleName})"];
        foreach (var diagnostic in emitResult.Diagnostics)
        {
            var pos = diagnostic.Location.GetLineSpan();
            logs.Add($"[{diagnostic.Severity}] {diagnostic.Id}: {diagnostic.GetMessage()} at {pos.Path} (Line: {pos.StartLinePosition.Line + 1}, Character: {pos.StartLinePosition.Character + 1})");
        }
        NebulaAndroid.NebulaLoader.MyPlugin.Log.LogMessage(string.Join('\n', logs));

        return emitResult.Success;
    }
}


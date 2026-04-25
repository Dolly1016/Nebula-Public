using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace AddonScriptCompiler;

public static class Compiler
{
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12);
    
    private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings("Virial", "Virial.Compat", "System", "System.Linq", "System.Collections.Generic")
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithMetadataImportOptions(MetadataImportOptions.All);

    public static bool Compile(
        string moduleName,
        string[] references,
        string sourceDir,
        string outputPath,
        bool useHiddenMembers)
    {
        List<SyntaxTree> trees = new();
        
        if (Directory.Exists(sourceDir))
        {
            var files = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string source = File.ReadAllText(file, Encoding.UTF8);
                // Make the path relative to sourceDir for diagnostic clarity
                string relativePath = Path.GetRelativePath(sourceDir, file);
                trees.Add(CSharpSyntaxTree.ParseText(source, ParseOptions, relativePath, Encoding.UTF8));
            }
        }

        if (trees.Count == 0)
        {
            // No code to compile
            return false;
        }

        var myCompilationOptions = CompilationOptions.WithModuleName(moduleName);

        if (useHiddenMembers)
        {
            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (topLevelBinderFlagsProperty != null)
            {
                topLevelBinderFlagsProperty.SetValue(myCompilationOptions, (uint)1 << 22);
            }
            trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"Nebula\")]\n[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"NebulaAPI\")]", ParseOptions, "", Encoding.UTF8));
        }

        var metadataReferences = references
            .Where(r => !string.IsNullOrWhiteSpace(r) && File.Exists(r))
            .Select(r => MetadataReference.CreateFromFile(r))
            .ToList();

        var compilation = CSharpCompilation.Create(moduleName, trees, metadataReferences, myCompilationOptions);

        using var dllStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        var emitResult = compilation.Emit(dllStream);

        // Write log
        string logPath = outputPath + ".log";
        var logBuilder = new StringBuilder();
        
        if (emitResult.Diagnostics.Length > 0)
        {
            logBuilder.AppendLine("Compile Log:");
            foreach (var diagnostic in emitResult.Diagnostics)
            {
                var pos = diagnostic.Location.GetLineSpan();
                logBuilder.AppendLine($"[{diagnostic.Severity}] {diagnostic.Id}: {diagnostic.GetMessage()} at {pos.Path} (Line: {pos.StartLinePosition.Line + 1}, Character: {pos.StartLinePosition.Character + 1})");
            }
        }

        if (emitResult.Success)
        {
            logBuilder.AppendLine("Compilation succeeded.");
        }
        else
        {
            logBuilder.AppendLine("Compilation failed.");
        }

        File.WriteAllText(logPath, logBuilder.ToString(), Encoding.UTF8);

        return emitResult.Success;
    }
}

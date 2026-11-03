using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AddonScriptCompiler;

public static class AssembleResolver
{
    public static void AddResolver()
    {
        Assembly.LoadFrom(Path.Combine("NebulaLibs", "System.Collections.Immutable.dll"));
        Assembly.LoadFrom(Path.Combine("NebulaLibs", "System.Reflection.Metadata.dll"));
        Assembly.LoadFrom(Path.Combine("NebulaLibs", "Microsoft.CodeAnalysis.dll"));
        Assembly.LoadFrom(Path.Combine("NebulaLibs", "Microsoft.CodeAnalysis.CSharp.dll"));
    }
}

internal static class Program
{
    public static int Main(string[] args)
    {
        AssembleResolver.AddResolver();
        return RunApp(args);
    }

    private static int RunApp(string[] args)
    {
        var references = new List<string>();
        string? moduleName = null;
        string? sourceDir = null;
        string? outputPath = null;
        bool useHiddenMembers = false;
        string? configPath = null;
        bool hello = false;
        int? virialVersion = null;

        // Parse CLI arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--hello":
                    hello = true;
                    break;
                case "--module-name":
                    if (i + 1 < args.Length) moduleName = args[++i];
                    break;
                case "--source-dir":
                    if (i + 1 < args.Length) sourceDir = args[++i];
                    break;
                case "--output":
                    if (i + 1 < args.Length) outputPath = args[++i];
                    break;
                case "--use-hidden-members":
                    useHiddenMembers = true;
                    break;
                case "--reference":
                    if (i + 1 < args.Length) references.Add(args[++i]);
                    break;
                case "--config":
                    if (i + 1 < args.Length) configPath = args[++i];
                    break;
                case "--virial-version":
                    if (i + 1 < args.Length) virialVersion = int.Parse(args[++i]);
                    break;
            }
        }

        if (hello)
        {
            Console.WriteLine("Hello.");
            return 0;
        }

        // Merge with Config File if provided (plain text, one path per line)
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            try
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        references.Add(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading config file: {ex.Message}");
                return -1;
            }
        }

        // Validate required arguments
        if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine("Missing required arguments. Need --module-name, --source-dir, and --output.");
            return -1;
        }

        try
        {
            bool success = Compiler.Compile(moduleName, references.ToArray(), sourceDir, outputPath, useHiddenMembers, virialVersion ?? 0);
            Console.WriteLine($"Finished!");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal compile error: {ex}");
            return -1;
        }
    }
}

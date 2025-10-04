using Mono.Cecil;
using System;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
        {
            Console.Error.WriteLine("Error: Assembly path not provided.");
            return;
        }

        var assemblyPath = args[0];
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Error: File not found at '{assemblyPath}'");
            return;
        }

        Console.WriteLine($"Processing assembly: {assemblyPath}");

        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadWrite = true });

        var resourcesToRename = assembly.MainModule.Resources
            .OfType<EmbeddedResource>()
            .Where(r => r.Name.Contains('\\'))
            .ToList();

        if (resourcesToRename.Count == 0)
        {
            Console.WriteLine("No resources with backslashes found to rename.");
            return;
        }

        foreach (var resource in resourcesToRename)
        {
            var originalName = resource.Name;
            var newName = originalName.Replace('\\', '.');

            resource.Name = newName;

            Console.WriteLine($"  Renamed '{originalName}' -> '{newName}'");
        }

        assembly.Write();
        Console.WriteLine("Assembly updated successfully.");
    }
}
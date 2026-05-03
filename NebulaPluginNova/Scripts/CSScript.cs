using Cpp2IL.Core.Extensions;
using Iced.Intel;
using Il2CppSystem.CodeDom.Compiler;
using Nebula.Patches;
#if ANDROID
using NebulaAndroid.CSScriptAndroid;
#endif
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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

internal record AddonScript(Assembly Assembly, NebulaAddon Addon, object? Reference, AddonBehaviour Behaviour);

[NebulaPreprocess(PreprocessPhase.CompileAddons)]
internal static class AddonScriptManagerLoader
{
    private static bool setUpDone = false;
    static internal string[] ReferenceAssemblies = [];
    internal static void SetUp()
    {
        if (setUpDone) return;
        setUpDone = true;

        ReferenceAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Select(a => a.Location).ToArray();
    }

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        Patches.LoadPatch.LoadingText = "Compiling Addon Scripts";
        yield return null;

        yield return AddonScriptManager.CoLoad(preprocessor);
    }
}

internal static class AddonScriptManager
{
    static public IEnumerable<AddonScript> ScriptAssemblies => scriptAssemblies;
    static private List<AddonScript> scriptAssemblies = [];
    static private Virial.Logging.ILogger Log;
    static private bool TryLoadCache(NebulaAddon addon, out string dllName, [MaybeNullWhen(false)] out string dllPath)
    {
        dllName = $"{addon.Id}_{addon.HandshakeHash.ToBase36()}.dll";

        var dirPath = PathHelpers.DllCacheDirPath;
        if (!Directory.Exists(PathHelpers.DllCacheDirPath))
        {
            dllPath = null;
            return false;
        }
        Directory.CreateDirectory(dirPath);

        dllPath = dirPath + Path.DirectorySeparatorChar + dllName;
        return File.Exists(dllPath);
        
    }

    static public IEnumerator CoCheckAndInstallRuntime(NebulaPreprocessor preprocessor)
    {
#if PC
        var psi = new System.Diagnostics.ProcessStartInfo()
        {
            FileName = $"Tools{Path.DirectorySeparatorChar}AddonScriptCompiler.exe",
            Arguments = $"--hello",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = PathHelpers.GameRootPath
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process != null)
        {
            while (!process.HasExited) yield return null;


            if (process.ExitCode == 0) yield break;

            preprocessor.SetLoadingText("Installing .NET Rutime...");
            yield return null;

            string command = "winget";
            string arguments = "install Microsoft.DotNet.DesktopRuntime.8 -e --silent --accept-source-agreements --accept-package-agreements";

            psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = true, 
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas"
            };

            process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                while (!process.HasExited) yield return null;
                yield break;
            }
        }
        else
        {
            Log.Error("Failed to start AddonScriptCompiler.exe");
        }
#else
        yield break;
#endif
    }

    static public IEnumerator CoLoad(NebulaPreprocessor preprocessor)
    {
        Log = NebulaAPI.Logging.NebulaLogger("Scripting");

        // Write references.txt at the start
        AddonScriptManagerLoader.SetUp();

        if (!Directory.Exists(PathHelpers.DllCacheDirPath)) Directory.CreateDirectory(PathHelpers.DllCacheDirPath);

#if PC
        string referencesPath = Path.Combine(PathHelpers.DllCacheDirPath, "references.txt");
        File.WriteAllLines(referencesPath, AddonScriptManagerLoader.ReferenceAssemblies);
#endif

        bool triedChecking = false;

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

            var dllFiles = addon.Archive.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".dll")).ToArray();
            if (dllFiles.Length > 0)
            {
                yield return preprocessor.SetLoadingText("Loading Addon Scripts\n" + addon.Id);

                foreach (var dll in dllFiles)
                {
                    using var stream = dll.Open();
                    var assembly = NebulaPlugin.NoSAssemblyContext.LoadFromStream(stream);
                    scriptAssemblies.Add(new(assembly, addon, null, addonBehaviour ?? new()));
                    NebulaAPI.Preprocessor?.PickUpPreprocess(assembly);
                }
            }
            else
            {
                if (!triedChecking)
                {
                    triedChecking = true;
                    yield return CoCheckAndInstallRuntime(preprocessor);
                }

                var sources = addon.Archive.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".cs")).ToArray();
                if (sources.Length == 0) continue;
                yield return preprocessor.SetLoadingText("Compiling Addon Scripts\n" + addon.Id);

                bool TryLoadFrom(string addonPath)
                {
                    try
                    {
                        var compiledAssembly = NebulaPlugin.NoSAssemblyContext.LoadFromAssemblyPath(addonPath);
                        scriptAssemblies.Add(new(compiledAssembly, addon, null, addonBehaviour ?? new()));
                        NebulaAPI.Preprocessor?.PickUpPreprocess(compiledAssembly);
                    }catch(Exception e)
                    {
                        return false;
                    }
                    return true;
                }

                if (!TryLoadCache(addon, out var dllName, out var foundPath) || !TryLoadFrom(foundPath))
                {
                    
                    string outputPath = Path.Combine(PathHelpers.DllCacheDirPath, dllName);
                    string moduleName = "Script." + addon.Id.HeadUpper();

#if PC
                    string tempDir = Path.Combine(PathHelpers.DllCacheDirPath, "temp");
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                    Directory.CreateDirectory(tempDir);
                    foreach (var srcEntry in sources)
                    {
                        string relativePath = srcEntry.FullName.Substring(prefix.Length).Replace("..", "Dir");
                        string outPath = Path.Combine(tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                        using var srcStream = srcEntry.Open();
                        using var dstStream = File.Create(outPath);
                        srcStream.CopyTo(dstStream);
                    }

                    var psi = new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = $"Tools{Path.DirectorySeparatorChar}AddonScriptCompiler.exe",
                        Arguments = $"--module-name \"{moduleName}\" --source-dir \"{tempDir}\" --output \"{outputPath}\" --config \"{referencesPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = PathHelpers.GameRootPath
                    };
                    if (addonBehaviour?.UseHiddenMembers ?? false) psi.Arguments += " --use-hidden-members";
                    
                    var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        while (!process.HasExited)
                        {
                            yield return null;
                        }

                        if (process.ExitCode == 0 && File.Exists(outputPath))
                        {
                            TryLoadFrom(outputPath);
                            Log.Message("Compile Finished (Addon: " + addon.Id + ")\n  " + process.StandardOutput.ReadToEnd().Replace("\n", "\n  "));
                        }
                        else
                        {
                            Log.Error("Compile Error! Scripts is ignored (Addon: " + addon.Id + ")\n  " + process.StandardError.ReadToEnd().Replace("\n", "\n  "));
                        }
                    }
                    else
                    {
                        Log.Error("Failed to start AddonScriptCompiler.exe (Addon: " + addon.Id + ")");
                    }
#else
                    var found = Compiler.Compile(moduleName, AddonScriptManagerLoader.ReferenceAssemblies, sources.Select(s => (s.FullName.Substring(prefix.Length), new StreamReader(s.Open()).ReadToEnd())).ToArray(), outputPath, addonBehaviour?.UseHiddenMembers ?? false);
                    if (found) TryLoadFrom(outputPath);
                    
#endif
                }
            }
        }
        
        // Clean up temp directory after compiling all addons
        string finalTempDir = Path.Combine(PathHelpers.DllCacheDirPath, "temp");
        if (Directory.Exists(finalTempDir))
        {
            Directory.Delete(finalTempDir, true);
        }

        yield break;
    }

}
using Cpp2IL.Core.Extensions;
using Iced.Intel;
using Nebula.Patches;
#if ANDROID
using NebulaAndroid.CSScriptAndroid;
#endif
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
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

internal record AddonScript(Assembly Assembly, NebulaAddon Addon, object? Reference, AddonBehaviour Behaviour, List<(Assembly LibAssembly, string LibPath)> Assemblies);

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

    static private bool TryLoadCache(string dllName, [MaybeNullWhen(false)] out string dllPath)
    {
        var dirPath = PathHelpers.DllCacheDirPath;
        dllPath = dirPath + Path.DirectorySeparatorChar + dllName;

        if (!Directory.Exists(PathHelpers.DllCacheDirPath))
        {
            dllPath = null;
            return false;
        }
        return File.Exists(dllPath);
    }

    static private bool TryLoadAddonCache(NebulaAddon addon, out string dllName, out string dllPath)
    {
        dllName = $"{addon.Id}_{addon.HandshakeHash.ToBase36()}.dll";
        return TryLoadCache(dllName, out dllPath);
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
            string libPrefix = addon.InZipPath + "Libraries/";

            AddonBehaviour? addonBehaviour = null;
            var behaviour = addon.Archive.GetEntry(prefix + ".behaviour");
            if (behaviour != null)
            {
                using var stream = behaviour.Open();
                addonBehaviour = JsonStructure.Deserialize<AddonBehaviour>(stream);
            }

            List<(Assembly LibAssembly, string LibPath)> loadedLibs = [];

            //依存先アドオンのアセンブリ
            foreach(var dep in addon.Dependency)
            {
                if(scriptAssemblies.Find(entry => entry.Addon == dep, out var depScript))
                {
                    foreach (var depLib in depScript.Assemblies)
                    {
                        if(!loadedLibs.Any(l => l.LibPath == depLib.LibPath)) loadedLibs.Add(depLib);
                    }
                }
            }

            bool TryLoadAddonFrom(string addonPath)
            {
                try
                {
                    var compiledAssembly = NebulaPlugin.NoSAssemblyContext.LoadFromAssemblyPath(addonPath);
                    loadedLibs.Add((compiledAssembly, addonPath));
                    scriptAssemblies.Add(new(compiledAssembly, addon, null, addonBehaviour ?? new(), loadedLibs));
                    NebulaAPI.Preprocessor?.PickUpPreprocess(compiledAssembly);
                }
                catch (Exception e)
                {
                    return false;
                }
                return true;
            }

            bool TryLoadFrom(string dllPath, out Assembly assembly)
            {
                try
                {
                    assembly = NebulaPlugin.NoSAssemblyContext.LoadFromAssemblyPath(dllPath);
                }
                catch (Exception e)
                {
                    assembly = null!;
                    return false;
                }
                return true;
            }

            var libraries = addon.Archive.Entries.Where(e => e.FullName.StartsWith(libPrefix) && e.FullName.EndsWith(".dll")).ToArray();
            if (libraries.Length > 0)
            {
                yield return preprocessor.SetLoadingText("Loading Addon Libraries\n" + addon.Id);

                foreach (var lib in libraries)
                {
                    var fileName = $"{addon.Id}_{addon.HandshakeHash.ToBase36()}_{lib.Name.Substring(libPrefix.Length, lib.Name.Length - libPrefix.Length - 4)}.dll";

                    if (!TryLoadCache(fileName, out var libPath) || !TryLoadFrom(libPath, out var libAssembly))
                    {
                        {
                            using var srcStream = lib.Open();
                            using var dstStream = File.Create(libPath);
                            srcStream.CopyTo(dstStream);
                            dstStream.Flush();
                        }
                        libAssembly = NebulaPlugin.NoSAssemblyContext.LoadFromAssemblyPath(libPath);
                    }

                    loadedLibs.Add((libAssembly, libPath));
                }
            }

            if (!triedChecking)
            {
                triedChecking = true;
                yield return CoCheckAndInstallRuntime(preprocessor);
            }

            var sources = addon.Archive.Entries.Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".cs")).ToArray();
            if (sources.Length == 0) continue;
            yield return preprocessor.SetLoadingText("Compiling Addon Scripts\n" + addon.Id);

            

            if (!TryLoadAddonCache(addon, out var dllName, out var outputPath) || !TryLoadAddonFrom(outputPath))
            {
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

                var args = $"--module-name \"{moduleName}\" --source-dir \"{tempDir}\" --output \"{outputPath}\" --config \"{referencesPath}\"";
                if (loadedLibs.Count > 0) args += " " + string.Join(" ", loadedLibs.Select(entry => $" --reference \"{entry.LibPath}\""));
                var psi = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = $"Tools{Path.DirectorySeparatorChar}AddonScriptCompiler.exe",
                    Arguments = args,
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
                        TryLoadAddonFrom(outputPath);
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
                if (found) TryLoadAddonFrom(outputPath);        
#endif
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
using Discord;
using Il2CppInterop.Runtime.Attributes;
using Mono.CSharp;
using Nebula;
using Nebula.Scripts;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Attributes;

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
        
        public CSScripting()
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
        }

        public bool Evaluate(string program, out string? errorText) => Evaluate(program, out errorText, out _);
        public bool Evaluate(string program, out string? errorText,out object? result)
        {
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

    static private CSScripting GetScripting(NebulaAddon addon)
    {
        if (!scriptings.TryGetValue(addon, out var scripting))
        {
            scripting = new CSScripting();
            scripting.Evaluate($"#define NOS_API_{Virial.NebulaAPI.APIVersion.Replace('.', '_')}\n", out _);
            scriptings.Add(addon, scripting);
        }
        return scripting;
    }

    static private void Evaluate(CSScripting scripting, ZipArchiveEntry program)
    {
        using var reader = new StreamReader(program.Open());
        if (!scripting.Evaluate(reader.ReadToEnd(), out var error))
            NebulaPlugin.Log.Print(null, "Error has occurred in " + program.Name + "\n" + error + "\n" + scripting.PopLogText());
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
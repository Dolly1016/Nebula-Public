using AmongUs.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Nebula.Patches;

internal static class MemoryLogger
{
    static private readonly MemoryStream log = new();
    static MemoryLogger()
    {
        log.Write([0xEF, 0xBB, 0xBF]);
    }
    public static void AppendLog(string text)
    {
        log.Write(Encoding.UTF8.GetBytes(text + "\n"));
    }
    public static byte[] ToByteArray() => log.ToArray();
}
[HarmonyPatch(typeof(Debug), nameof(Debug.Log), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockLogPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockLogWarningPatch
{
    public static bool Prefix([HarmonyArgument(0)]Il2CppSystem.Object message)
    {
        MemoryLogger.AppendLog("[UnityEngine, Warning]" + message.ToString());
        return DebugTools.AllowVanillaLog;
    }
}

[HarmonyPatch(typeof(Debug), nameof(Debug.LogError), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockLogErrorPatch {
    public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Object message)
    {
        MemoryLogger.AppendLog("[UnityEngine, Error]" + message.ToString());
        return DebugTools.AllowVanillaLog;
    }
}

[HarmonyPatch(typeof(Debug), nameof(Debug.LogException), typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object))]
public static class BlockLogExceptionPatch {
    public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Exception exception)
    {
        MemoryLogger.AppendLog("[UnityEngine, Exception]" + exception.ToString());
        return DebugTools.AllowVanillaLog;
    }
}

[HarmonyPatch(typeof(Debug), nameof(Debug.Log), typeof(Il2CppSystem.Object))]
public static class BlockLogShortPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), typeof(Il2CppSystem.Object))]
public static class BlockLogWarningShortPatch
{
    public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Object message)
    {
        MemoryLogger.AppendLog("[UnityEngine, Warning]" + message.ToString());
        return DebugTools.AllowVanillaLog;
    }
}

[HarmonyPatch(typeof(Debug), nameof(Debug.LogError), typeof(Il2CppSystem.Object))]
public static class BlockLogErrorShortPatch
{
    public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Object message)
    {
        MemoryLogger.AppendLog("[UnityEngine, Error]" + message.ToString());
        return DebugTools.AllowVanillaLog;
    }
}
[HarmonyPatch(typeof(Debug), nameof(Debug.LogException), typeof(Il2CppSystem.Exception))]
public static class BlockLogExceptionShortPatch
{
    public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Exception exception)
    {
        MemoryLogger.AppendLog("[UnityEngine, Exception]" + exception.ToString());
        return DebugTools.AllowVanillaLog;
    }
}


[HarmonyPatch(typeof(Logger), nameof(Logger.Debug), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerDebugPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Logger), nameof(Logger.Info), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerInfoPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Logger), nameof(Logger.Warning), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerWarningPatch {
    public static bool Prefix([HarmonyArgument(1)] Il2CppSystem.Object message)
    {
        MemoryLogger.AppendLog("[AULogger, Warning]" + message.ToString());
        return DebugTools.AllowVanillaLog;
    }
}

[HarmonyPatch(typeof(Logger), nameof(Logger.Error), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerErrorPatch
{
    public static bool Prefix([HarmonyArgument(1)] Il2CppSystem.Object message)
    {
        MemoryLogger.AppendLog("[AULogger, Error]" + message.ToString());
        return DebugTools.AllowVanillaLog;
    }
}


[HarmonyPatch(typeof(Logger), nameof(Logger.Exception), typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object))]
public static class BlockAULoggerExceptionPatch
{
    public static bool Prefix([HarmonyArgument(0)] Il2CppSystem.Exception e)
    {
        MemoryLogger.AppendLog("[AULogger, Exception]" + e.ToString());
        return DebugTools.AllowVanillaLog;
    }
}


[HarmonyPatch(typeof(AbstractUserSaveData), nameof(AbstractUserSaveData.HandleSave))]
public static class BlockSaveUserDataPatch { public static bool Prefix() => NebulaPlugin.Log.IsPreferential; }
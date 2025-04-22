using AmongUs.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Nebula.Patches;

[HarmonyPatch(typeof(Debug), nameof(Debug.Log), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockLogPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockLogWarningPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogError), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockLogErrorPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogException), typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object))]
public static class BlockLogExceptionPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.Log), typeof(Il2CppSystem.Object))]
public static class BlockLogShortPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), typeof(Il2CppSystem.Object))]
public static class BlockLogWarningShortPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogError), typeof(Il2CppSystem.Object))]
public static class BlockLogErrorShortPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Debug), nameof(Debug.LogException), typeof(Il2CppSystem.Exception))]
public static class BlockLogExceptionShortPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }


[HarmonyPatch(typeof(Logger), nameof(Logger.Debug), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerDebugPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Logger), nameof(Logger.Info), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerInfoPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Logger), nameof(Logger.Warning), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerWarningPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Logger), nameof(Logger.Error), typeof(Il2CppStringArray), typeof(Il2CppSystem.Object), typeof(UnityEngine.Object))]
public static class BlockAULoggerErrorPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(Logger), nameof(Logger.Exception), typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object))]
public static class BlockAULoggerExceptionPatch { public static bool Prefix() => DebugTools.AllowVanillaLog; }

[HarmonyPatch(typeof(AbstractUserSaveData), nameof(AbstractUserSaveData.HandleSave))]
public static class BlockSaveUserDataPatch { public static bool Prefix() => NebulaPlugin.Log.IsPreferential; }
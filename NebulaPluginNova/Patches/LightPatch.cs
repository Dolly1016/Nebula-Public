using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Rewired.Utils.Platforms.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
public class LightPatch
{
    public static float lastRange = 1f;

    public static bool Prefix(ref float __result, ShipStatus __instance, [HarmonyArgument(0)] GameData.PlayerInfo? player)
    {
        if (__instance == null)
        {
            lastRange = 1f;
            return true;
        }

        if (player == null || player.IsDead)
        {
            __result = __instance.MaxLightRadius;
            return false;
        }

        if ((NebulaGameManager.Instance?.GameState ?? NebulaGameStates.NotStarted) == NebulaGameStates.NotStarted) return true;

        ISystemType? systemType = __instance.Systems.ContainsKey(SystemTypes.Electrical) ? __instance.Systems[SystemTypes.Electrical] : null;
        SwitchSystem? switchSystem = systemType?.TryCast<SwitchSystem>();

        float t = (float)(switchSystem?.Value ?? 255f) / 255f;

        var info = PlayerControl.LocalPlayer.GetModInfo();
        bool hasImpostorVision = info?.Role.HasImpostorVision ?? false;
        bool ignoreBlackOut = info?.Role.IgnoreBlackout ?? true;

        if (ignoreBlackOut) t = 1f;

        float radiusRate = Mathf.Lerp(__instance.MinLightRadius, __instance.MaxLightRadius, t);
        float range = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(hasImpostorVision ? FloatOptionNames.ImpostorLightMod : FloatOptionNames.CrewLightMod);
        float rate = 1f;
        info?.AssignableAction(r=>r.EditLightRange(ref rate));
        rate = Mathf.Lerp(lastRange, rate, 0.7f * Time.deltaTime);
        lastRange = rate;
        __result = radiusRate * range * rate;

        return false;
    }
}

//影貫通

[HarmonyPatch(typeof(LightSourceGpuRenderer), nameof(LightSourceGpuRenderer.GPUShadows))]
public static class LightSourceGpuRendererPatch
{
    static Il2CppReferenceArray<Collider2D> origArray = null!;
    static Il2CppReferenceArray<Collider2D> zeroArray = new(0);

    public static void Prefix(LightSourceGpuRenderer __instance)
    {
        origArray = __instance.hits;
        if (NebulaGameManager.Instance?.IgnoreWalls ?? false) __instance.hits = zeroArray;
    }

    public static void Postfix(LightSourceGpuRenderer __instance)
    {
        if (__instance.hits != origArray) __instance.hits = origArray;
    }
}

[HarmonyPatch(typeof(LightSourceRaycastRenderer), nameof(LightSourceRaycastRenderer.RaycastShadows))]
public static class LightSourceRaycastRendererPatch
{
    static Il2CppReferenceArray<Collider2D> origArray = null!;
    static Il2CppReferenceArray<Collider2D> zeroArray = new(0);

    public static void Prefix(LightSourceRaycastRenderer __instance)
    {
        origArray = __instance.hits;
        if (NebulaGameManager.Instance?.IgnoreWalls ?? false) __instance.hits = zeroArray;
    }

    public static void Postfix(LightSourceGpuRenderer __instance)
    {
        if (__instance.hits != origArray) __instance.hits = origArray;
    }
}
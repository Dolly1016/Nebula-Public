using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
public class LightPatch
{
    public static float lastRange = 1f;
    public static float lastRangeForDive = 1f;

    public static bool Prefix(ref float __result, ShipStatus __instance, [HarmonyArgument(0)] NetworkedPlayerInfo? player)
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

        var info = GamePlayer.LocalPlayer;
        var modinfo = info?.Unbox();
        bool hasImpostorVision = modinfo?.Role.HasImpostorVision ?? false;
        bool ignoreBlackOut = (modinfo?.Role.IgnoreBlackout ?? true) || (info?.AllAbilities.Any(a => a.IgnoreBlackout) ?? false);

        if (ignoreBlackOut) t = 1f;

        float radiusRate = Mathf.Lerp(__instance.MinLightRadius, __instance.MaxLightRadius, t);
        float range = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(hasImpostorVision ? FloatOptionNames.ImpostorLightMod : FloatOptionNames.CrewLightMod);
        float rate = GameOperatorManager.Instance?.Run(new LightRangeUpdateEvent(1f)).LightRange ?? 1f;
        rate *= GamePlayer.LocalPlayer?.Unbox().CalcAttributeVal(PlayerAttributes.Eyesight) ?? 1f;

        lastRange -= (lastRange - rate).Delta(0.7f, 0.005f);
        __result = radiusRate * range * lastRange;

        if (info?.IsDived ?? false)
            lastRangeForDive = 0f;
        else
            lastRangeForDive += (1f - lastRangeForDive).Delta(6f, 0.005f);

        __result *= lastRangeForDive;

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

[HarmonyPatch(typeof(OneWayShadows), nameof(OneWayShadows.IsIgnored))]
public static class OneWayShadowsPatch
{
    public static bool Prefix(OneWayShadows __instance, ref bool __result, [HarmonyArgument(0)] LightSource lightSource)
    {
        var info = GamePlayer.LocalPlayer;
        if (info == null) return true;

        __result = (__instance.IgnoreImpostor && info.Role.HasImpostorVision) || __instance.RoomCollider.OverlapPoint(lightSource.transform.position);
        return false;
    }
}
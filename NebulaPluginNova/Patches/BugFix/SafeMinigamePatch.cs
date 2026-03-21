using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Virial;

namespace Nebula.Patches.BugFix;

/// <summary>
/// 高リフレッシュレートで垂直同期がオンのとき、回す向きの判定が異常に厳しくなる問題。
/// </summary>
[HarmonyPatch(typeof(SafeMinigame), nameof(SafeMinigame.Update))]
internal class SafeMinigameUpdatePatch
{
    static private float elapsedTime = 0f;
    static bool Prefix(SafeMinigame __instance)
    {
        if (__instance.latched[2]) return true; //回転のフェイズが終わっていたらあまり気にしなくてよい。

        if (AmongUsUtil.IsInFastUpdate)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime < AmongUsUtil.DeltaTime70) return false;
            elapsedTime = 0f;
            return true;
        }
        return true;
    }
}

[HarmonyPatch(typeof(SafeMinigame), nameof(SafeMinigame.AngleNear))]
internal class SafeMinigameAngleNearPatch
{
    static void Prefix(SafeMinigame __instance, [HarmonyArgument(0)]ref float actual)
    {
        if (actual > 360f) actual -= 360f;
    }
}

[HarmonyPatch(typeof(SafeMinigame), nameof(SafeMinigame.CheckTumblr))]
internal class SafeMinigameCheckTumblrPatch
{
    static void Prefix(SafeMinigame __instance, [HarmonyArgument(1)] ref float tumRotZ)
    {
        if (tumRotZ > 360f) tumRotZ -= 360f;
    }
}
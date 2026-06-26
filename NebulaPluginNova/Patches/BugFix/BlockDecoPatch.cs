using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.BugFix;

/// <summary>
/// デコレーションがマップの計上を変えてしまう問題。
/// デコレーションを一律ブロックして対応する。
/// </summary>
//[HarmonyPatch(typeof(MapDecor), nameof(MapDecor.Awake))] // v17.3互換を保つため、パッチングは別の方法を取る。
internal static class MapDecorAwakePatch
{
    static bool Prefix(MapDecor __instance)
    {
        foreach(var decor in __instance.AllDecor)
        {
            decor.gameObject.SetActive(false);
        }
        return false;
    }
}


internal static class MapDecorAwakePatcher
{
    static public void TryApply(Harmony harmony)
    {
        var auType = typeof(AmongUsClient);
        var assembly = auType.Assembly;
        var type = assembly.GetType("MapDecor");
        if (type == null) return;
        var method = AccessTools.Method(type, "Awake");
        if (method == null) return;
        var prefix = AccessTools.Method(typeof(MapDecorAwakePatch), "Prefix");
        HarmonyLib.HarmonyMethod harmonyMethod = new(prefix);
        harmony.Patch(method, harmonyMethod);
    }
}
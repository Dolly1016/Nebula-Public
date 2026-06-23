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
[HarmonyPatch(typeof(MapDecor), nameof(MapDecor.Awake))]
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

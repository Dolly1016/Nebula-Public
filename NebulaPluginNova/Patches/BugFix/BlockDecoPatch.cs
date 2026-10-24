using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;

namespace Nebula.Patches.BugFix;

/// <summary>
/// デコレーションがマップの形状を変えてしまう問題。
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

[HarmonyPatch(typeof(DateHide), nameof(DateHide.Awake))]
internal static class DateHideAwakePatch
{
    static bool Prefix(MapDecor __instance)
    {
        if (NebulaAPI.AmongUs.MapId == 5 && GeneralConfigurations.FungleVariation >= 1)
        {
            __instance.gameObject.SetActive(false);
            return false;
        }
        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.Cosmetics;

[HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.SetVisor), typeof(string), typeof(int))]
public class VisorLayerSetVisorPatch
{
    public static bool Prefix(VisorLayer __instance, [HarmonyArgument(0)] string visorId, [HarmonyArgument(1)] int colorId)
    {
        if (!DestroyableSingleton<HatManager>.InstanceExists) return false;

        VisorData visorById = DestroyableSingleton<HatManager>.Instance.GetVisorById(visorId);
        __instance.SetVisor(visorById, colorId);
        return false;
    }
}
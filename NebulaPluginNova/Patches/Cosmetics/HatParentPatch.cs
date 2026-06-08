using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.Cosmetics;

[HarmonyPatch(typeof(HatParent), nameof(HatParent.SetHat), typeof(string), typeof(int))]
public class HatParentSetHatPatch
{
    public static bool Prefix(HatParent __instance, [HarmonyArgument(0)] string hatId, [HarmonyArgument(1)] int color)
    {
        if (!DestroyableSingleton<HatManager>.InstanceExists) return false;
        
        HatData hatById = DestroyableSingleton<HatManager>.Instance.GetHatById(hatId);
        __instance.SetHat(hatById, color);
        return false;
    }
}
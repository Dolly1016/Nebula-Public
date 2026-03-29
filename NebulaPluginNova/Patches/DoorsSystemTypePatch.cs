using Hazel;
using Nebula.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

[HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.UpdateSystem))]
internal class DoorsSystemTypeUpdateSystemPatch
{
    static bool Prefix(DoorsSystemType __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
    {
        var b = msgReader.ReadByte();
        if ((b & 64) == 0) return false;
        AdditionalDoors.UpdateDoorSystem(__instance, player, (byte)(b & 63));
        return false;
    }
}

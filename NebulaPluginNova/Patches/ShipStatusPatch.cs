using Hazel;
using Nebula.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RpcUpdateSystem), typeof(SystemTypes), typeof(byte))]
internal class ShipStatusRpcUpdateSystemPatch
{
    static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] byte amount)
    {
        if (!AdditionalDoors.RpcUpdateDoorSystem(systemType, amount)) return false;
        return true;
    }
}

using MonoMod.Cil;
using System.Reflection;

namespace Nebula.Patches;

[HarmonyPatch]
[NebulaRPCHolder]
public static class UpdateDoorPatch
{
    static private bool InUpdateFunc = false;
    static private int TargetDoorId = 0;
    static private Minigame instance = null!;
    static private RemoteProcess<int> RpcUpdateExtraDoor = new("UpdateExtraDoor", (id, _) =>
    {
        if (ShipStatus.Instance.AllDoors.Find(d => d.Id == id, out var door)) door.SetDoorway(true);
    });

    static void ModRpcUpdateSystem(ShipStatus shipStatus, SystemTypes systemType, byte amount)
    {
        if (TargetDoorId >= 32)
        {
            RpcUpdateExtraDoor.Invoke(TargetDoorId);
        }
        else
        {
            shipStatus.RpcUpdateSystem(systemType, amount);
        }
    }

    [HarmonyPatch(typeof(DoorCardSwipeGame), nameof(DoorCardSwipeGame.Update))]
    public static class DoorCardSwipeGamePatch
    {
        static void Prefix(DoorCardSwipeGame __instance)
        {
            instance = __instance;
            TargetDoorId = 0;
            if (__instance.MyDoor) TargetDoorId = __instance.MyDoor.Id;
            InUpdateFunc = true;
        }

        static void Postfix(DoorCardSwipeGame __instance)
        {
            TargetDoorId = 0;
            if (__instance.MyDoor) TargetDoorId = __instance.MyDoor.Id;
            InUpdateFunc = false;
        }
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RpcUpdateSystem), typeof(SystemTypes), typeof(byte))]
    public static class ShipStatusPatch
    {
        
        static bool Prefix(ShipStatus __instance, [HarmonyArgument(0)]SystemTypes systemType,[HarmonyArgument(1)] byte amount)
        {
            if(InUpdateFunc && systemType== SystemTypes.Doors && Minigame.Instance && instance && Minigame.Instance.GetInstanceID() == __instance.GetInstanceID())
            {
                ModRpcUpdateSystem(__instance, systemType, amount);
                return false;
            }
            return true;
        }
    }
}
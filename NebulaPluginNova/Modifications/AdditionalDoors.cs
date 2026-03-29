using Rewired.Utils.Classes.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game.Door;

namespace Nebula.Modifications;

/// <summary>
/// 追加ドアの処理とドア通知
/// </summary>
[NebulaRPCHolder]
internal static class AdditionalDoors
{
    internal static bool RpcUpdateDoorSystem(SystemTypes systemType, byte amount)
    {
        if(systemType != SystemTypes.Doors) return true;
        if ((amount & 64) == 0) return true;
        var doorId = amount & 63;
        OpenableDoor openableDoor = ShipStatus.Instance.AllDoors.First(d => d.Id == doorId);
        var ev = GameOperatorManager.Instance?.Run(new PlayerTryOpenDoorLocalEvent(GamePlayer.LocalPlayer!, openableDoor));
        if (ev?.IsCanceled ?? false) return false;
        return true;
    }

    internal static void UpdateDoorSystem(DoorsSystemType doorSystem, PlayerControl player, byte doorId)
    {
        OpenableDoor openableDoor = ShipStatus.Instance.AllDoors.First(d => d.Id == doorId);
        if (openableDoor != null)
        {
            if (openableDoor.IsOpen) return;
            var modPlayer = GamePlayer.GetPlayer(player.PlayerId)!;
            var ev = GameOperatorManager.Instance?.Run(new PlayerTryOpenDoorHostEvent(modPlayer, openableDoor));
            if (ev?.IsCanceled ?? false) return;
            openableDoor.SetDoorway(true);
            RpcNoticeDoorOpen.Invoke((modPlayer, doorId));
            doorSystem.IsDirty = true;
        }
    }

    private static readonly RemoteProcess<(GamePlayer player, byte doorId)> RpcNoticeDoorOpen = new("noticeDoorOpen", (message, _) =>
    {
        var door = ShipStatus.Instance.AllDoors.First(d => d.Id == message.doorId);
        GameOperatorManager.Instance?.Run(new PlayerOpenDoorEvent(message.player, door));
    }, true);
}


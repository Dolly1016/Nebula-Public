using Hazel;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nebula.Game.Statistics;
using System.Text;
using Virial.Game;

namespace Nebula.Patches;

[HarmonyPatch(typeof(HqHudOverrideTask), nameof(HqHudOverrideTask.FixedUpdate))]
public static class HqCommSabotagePatch
{
    static bool Prefix(HqHudOverrideTask __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Comms) ?? false)
        {
            return false;
        }

        return true;
    }
}

//偽タスクでは常に見た目上の解決中プレイヤーが0人になる
[HarmonyPatch(typeof(HqHudOverrideTask), nameof(HqHudOverrideTask.AppendTaskText))]
public static class HqCommSabotageTextPatch
{
    static bool Prefix(HqHudOverrideTask __instance, [HarmonyArgument(0)]StringBuilder sb)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Comms) ?? false)
        {
            __instance.even = !__instance.even;
            Color color = __instance.even ? Color.yellow : Color.red;
            sb.Append(color.ToTextColor());
            sb.Append(DestroyableSingleton<TranslationController>.Instance.GetString(TaskTypes.FixComms));
            sb.Append(" (0/2)");
            sb.Append("</color>");
            for (int i = 0; i < __instance.Arrows.Length; i++) __instance.Arrows[i].image.color = color;
            
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(HudOverrideTask), nameof(HudOverrideTask.FixedUpdate))]
public static class CommSabotagePatch
{
    static bool Prefix(HudOverrideTask __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Comms) ?? false)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.FixedUpdate))]
public static class LightSabotagePatch
{
    static bool Prefix(ElectricTask __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Electrical) ?? false)
        {
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ReactorTask), nameof(ReactorTask.FixedUpdate))]
public static class ReactorSabotagePatch
{
    static bool Prefix(ReactorTask __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Reactor, SystemTypes.Laboratory) ?? false)
        {
            if(!(__instance.reactor.Countdown > 0f))
            {
                var localPlayer = GamePlayer.LocalPlayer;
                if(!localPlayer.IsDead) localPlayer.Suicide(PlayerState.Deranged, EventDetail.FakeSabotage, KillParameter.NormalKill);
                FakeSabotageStatus.RpcRemoveMyFakeSabotage(SystemTypes.Reactor, SystemTypes.Laboratory);
                __instance.reactor.ClearSabotage();
            }
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(NoOxyTask), nameof(NoOxyTask.FixedUpdate))]
public static class NoOxySabotagePatch
{
    static bool Prefix(NoOxyTask __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.LifeSupp) ?? false)
        {
            if (!(__instance.reactor.Countdown > 0f))
            {
                var localPlayer = GamePlayer.LocalPlayer;
                if (!localPlayer.IsDead) localPlayer.Suicide(PlayerState.Deranged, EventDetail.FakeSabotage, KillParameter.NormalKill);
                FakeSabotageStatus.RpcRemoveMyFakeSabotage(SystemTypes.LifeSupp);
                __instance.reactor.Countdown = 10000f;
            }
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(HeliCharlesTask), nameof(HeliCharlesTask.FixedUpdate))]
public static class HeliSabotagePatch
{
    static bool Prefix(HeliCharlesTask __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.HeliSabotage) ?? false)
        {
            if (!(__instance.sabotage.Countdown > 0f))
            {
                var localPlayer = GamePlayer.LocalPlayer;
                if (!localPlayer.IsDead) localPlayer.Suicide(PlayerState.Deranged, EventDetail.FakeSabotage, KillParameter.NormalKill);
                FakeSabotageStatus.RpcRemoveMyFakeSabotage(SystemTypes.HeliSabotage);
                __instance.sabotage.ClearSabotage();
            }
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
public static class ReactorUpdateSystemPatch
{
    static bool Prefix(ReactorSystemType __instance)
    {
        FakeSabotageStatus.RpcRemoveMyFakeSabotage(SystemTypes.Reactor,SystemTypes.Laboratory);
        return true;
    }
}

[HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.UpdateSystem))]
public static class LifeSuppUpdateSystemPatch
{
    static bool Prefix(LifeSuppSystemType __instance, [HarmonyArgument(1)] MessageReader msgReader)
    {
        msgReader.PeekByte();
        FakeSabotageStatus.RpcRemoveMyFakeSabotage(SystemTypes.LifeSupp);
        return true;
    }
}

[HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.UpdateSystem))]
public static class HeliUpdateSystemPatch
{
    static bool Prefix(HeliSabotageSystem __instance)
    {
        FakeSabotageStatus.RpcRemoveMyFakeSabotage(SystemTypes.HeliSabotage);
        return true;
    }
}


[HarmonyPatch(typeof(SwitchSystem), nameof(SwitchSystem.Deteriorate))]
public static class SwitchSystemPatch
{
    static bool Prefix(SwitchSystem __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Electrical) ?? false)
        {
            __instance.Value = (byte)Math.Max((int)(__instance.Value - 3), 0);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(SwitchSystem), nameof(SwitchSystem.Deserialize))]
public static class SwitchSystemDeserializePatch
{
    static bool Prefix(SwitchSystem __instance, [HarmonyArgument(0)]MessageReader reader)
    {
        __instance.ExpectedSwitches = reader.ReadByte();
        __instance.ActualSwitches = reader.ReadByte();
        return false;
    }
}

[HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.Deteriorate))]
public static class ReactorSystemTypeDeterioratePatch
{
    static bool Prefix(ReactorSystemType __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.Reactor, SystemTypes.Laboratory) ?? false)
        {
            __instance.Countdown -= Time.deltaTime;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.Deteriorate))]
public static class LifeSuppSystemTypeDeterioratePatch
{
    static bool Prefix(LifeSuppSystemType __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.LifeSupp) ?? false)
        {
            __instance.Countdown -= Time.deltaTime;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.Deteriorate))]
public static class HeliSystemTypeDeterioratePatch
{
    static bool Prefix(HeliSabotageSystem __instance)
    {
        if (NebulaGameManager.Instance?.LocalFakeSabotage?.HasFakeSabotage(SystemTypes.HeliSabotage) ?? false)
        {
            __instance.Countdown -= Time.deltaTime;
            return false;
        }
        return true;
    }
}


[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
public static class SwitchSabotagePatch
{
    static bool Prefix(SabotageSystemType __instance, [HarmonyArgument(1)] MessageReader msgReader)
    {
        if (!(__instance.Timer > 0f) && !MeetingHud.Instance && (SystemTypes)msgReader.PeekByte() == SystemTypes.Electrical && AmongUsClient.Instance.AmHost)
        {
            __instance.IsDirty = true;
            byte b = (byte)(System.Random.Shared.Next((1 << 6) - 1) + 1);
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Electrical, (byte)(b | 128));
            __instance.Timer = 30f;
            __instance.IsDirty = true;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlainDoor), nameof(PlainDoor.SetDoorway))]
public static class InvalidDoorPlainPatch
{
    static bool Prefix(PlainDoor __instance, [HarmonyArgument(0)] bool open)
    {
        if (!open && __instance.TryGetComponent<InvalidDoor>(out _)) return false;
        return true;
    }
}

[HarmonyPatch(typeof(AutoOpenDoor), nameof(AutoOpenDoor.SetDoorway))]
public static class InvalidDoorAutoOpenPatch
{
    static bool Prefix(AutoOpenDoor __instance, [HarmonyArgument(0)] bool open)
    {
        if (!open && __instance.TryGetComponent<InvalidDoor>(out _)) return false;
        return true;
    }
}

[HarmonyPatch(typeof(MushroomWallDoor), nameof(MushroomWallDoor.SetDoorway))]
public static class InvalidDoorMushroomPatch
{
    static bool Prefix(MushroomWallDoor __instance, [HarmonyArgument(0)] bool open)
    {
        if (!open && __instance.TryGetComponent<InvalidDoor>(out _)) return false;
        return true;
    }
}
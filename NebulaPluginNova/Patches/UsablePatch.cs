﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using HarmonyLib;
using NAudio.MediaFoundation;
using Nebula.Events;
using Nebula.Player;

namespace Nebula.Patches;

[HarmonyPatch(typeof(KeyboardJoystick),nameof(KeyboardJoystick.HandleHud))]
public static class KeyboardInputPatch
{
    public static bool Prefix(KeyboardJoystick __instance)
    {
        if (!DestroyableSingleton<HudManager>.InstanceExists) return false;
        
        if (KeyboardJoystick.player.GetButtonDown(7)) HudManager.Instance.ReportButton.DoClick();
        
        if (KeyboardJoystick.player.GetButtonDown(6)) HudManager.Instance.UseButton.DoClick();
        
        if (KeyboardJoystick.player.GetButtonDown(4) && !HudManager.Instance.Chat.IsOpenOrOpening)
            HudManager.Instance.ToggleMapVisible(GameManager.Instance.GetMapOptions());

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Kill).KeyDown && HudManager.Instance.KillButton.gameObject.active) HudManager.Instance.KillButton.DoClick();
        if (KeyboardJoystick.player.GetButtonDown(50) && HudManager.Instance.ImpostorVentButton.gameObject.active) HudManager.Instance.ImpostorVentButton.DoClick();

        return false;
    }
}


[HarmonyPatch(typeof(Vent),nameof(Vent.CanUse))]
public static class VentCanUsePatch
{
    public static bool Prefix(Vent __instance, ref float __result,[HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
    {
        couldUse = true;
        
        float num = float.MaxValue;
        PlayerControl @object = pc.Object;
        PlayerModInfo? modInfo = NebulaGameManager.Instance?.GetModPlayerInfo(pc.PlayerId);

        if (@object.inVent && Vent.currentVent == __instance)
        {
            //既にベント内にいる場合
        }
        else {
            //ベント外にいる場合
            couldUse &= (modInfo?.Role.CanUseVent ?? false) || @object.inVent || @object.walkingToVent;
            if (modInfo?.Role.HasAnyTasks ?? false) couldUse &= !@object.MustCleanVent(__instance.Id);
            couldUse &= !pc.IsDead && @object.CanMove;
        }

        ISystemType systemType;
        if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out systemType))
        {
            VentilationSystem ventilationSystem = systemType.Cast<VentilationSystem>();
            if (ventilationSystem != null && ventilationSystem.IsVentCurrentlyBeingCleaned(__instance.Id)) couldUse = false;
        }

        canUse = couldUse;

        if (canUse)
        {
            Vector3 center = @object.Collider.bounds.center;
            Vector3 position = __instance.transform.position;
            num = Vector2.Distance(center, position);
            canUse &= (num <= __instance.UsableDistance && !PhysicsHelpers.AnythingBetween(@object.Collider, center, position, Constants.ShipOnlyMask, false));
        }
        __result = num;
        return false;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
public static class VentSetOutlinePatch
{
    public static bool Prefix(Vent __instance, [HarmonyArgument(0)]bool on, [HarmonyArgument(1)]bool mainTarget) {
        Color color = PlayerControl.LocalPlayer.GetModInfo()!.Role.Role.RoleColor;
        __instance.myRend.material.SetFloat("_Outline", (float)(on ? 1 : 0));
        __instance.myRend.material.SetColor("_OutlineColor", color);
        __instance.myRend.material.SetColor("_AddColor", mainTarget ? color : Color.clear);

        return false;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
public static class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        EventManager.HandleEvent(new Virial.Events.Player.PlayerExitVentEvent(pc.GetModInfo()!));

        pc.GetModInfo()?.Role.OnEnterVent(__instance);
        NebulaGameManager.Instance?.AllRoleAction(r => r.OnEnterVent(pc, __instance));
        pc.GetModInfo()?.Role.VentDuration?.Start();
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
public static class ExitVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        EventManager.HandleEvent(new Virial.Events.Player.PlayerEnterVentEvent(pc.GetModInfo()!));

        pc.GetModInfo()?.Role.OnExitVent(__instance);
        NebulaGameManager.Instance?.AllRoleAction(r => r.OnExitVent(pc, __instance));
        pc.GetModInfo()?.Role.VentCoolDown?.Start();
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.Use))]
public static class VentUsePatch
{
    public static bool Prefix(Vent __instance)
    {
        __instance.CanUse(PlayerControl.LocalPlayer.Data, out var flag, out _);
        if (!flag) return false;

        PlayerControl localPlayer = PlayerControl.LocalPlayer;
        bool isNotEnter = localPlayer.inVent && !localPlayer.walkingToVent;
        if (isNotEnter)
            localPlayer.MyPhysics.RpcExitVent(__instance.Id);
        if (!localPlayer.walkingToVent)
            localPlayer.MyPhysics.RpcEnterVent(__instance.Id);

        __instance.SetButtons(!isNotEnter && localPlayer.GetModInfo()!.Role.CanMoveInVent);
        return false;
    }
}

[HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
public static class ConsoleCanUsePatch
{
    public static bool Prefix(ref float __result, Console __instance, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
    {
        canUse = couldUse = false;

        var info = NebulaGameManager.Instance?.GetModPlayerInfo(PlayerControl.LocalPlayer.PlayerId);
        if (info == null) return true;

        if (ShipStatus.Instance.SpecialTasks.Any((task) => __instance.TaskTypes.Contains(task.TaskType)))
        {
            if (
                (__instance.TaskTypes.Contains(TaskTypes.FixLights) && info.AllAssigned().Any(assignable => !assignable.CanFixLight)) ||
                (__instance.TaskTypes.Contains(TaskTypes.FixComms) && info.AllAssigned().Any(assignable => !assignable.CanFixComm))
                )
            {
                __result = float.MaxValue;
                return false;
            }
        }

        if (__instance.AllowImpostor) return true;

        if (!info.Role.HasAnyTasks)
        {
            __result = float.MaxValue;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.CanUse))]
public static class SystemConsoleCanUsePatch
{
    public static void Postfix(SystemConsole __instance, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] ref bool canUse, [HarmonyArgument(2)] ref bool couldUse)
    {
        var info = NebulaGameManager.Instance?.GetModPlayerInfo(PlayerControl.LocalPlayer.PlayerId);
        if (info == null) return;

        //緊急会議コンソールの使用をブロック
        if(__instance.MinigamePrefab.TryCast<EmergencyMinigame>() && info.AllAssigned().Any(a => !a.CanCallEmergencyMeeting))
        {
            canUse = false;
            couldUse = false;
        }
    }
}

[HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.MeetingCalled))]
class MovingPlatformBehaviourMeetingCalledPatch
{
    static bool Prefix(MovingPlatformBehaviour __instance)
    {
        if (AmongUsUtil.CurrentMapId != 4) return true;
        return !GeneralConfigurations.AirshipOneWayMeetingRoomOption;
    }
}

[HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.InUse), MethodType.Getter)]
class CanUseMovingPlayformPatch
{
    static bool Prefix(MovingPlatformBehaviour __instance)
    {
        if (AmongUsUtil.CurrentMapId != 4) return true;
        return !GeneralConfigurations.AirshipOneWayMeetingRoomOption;
    }
}

[HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.SetSide))]
class MovingPlatformBehaviourSetSidePatch
{
    static bool Prefix(MovingPlatformBehaviour __instance)
    {
        __instance.IsDirty = true;
        return !GeneralConfigurations.AirshipOneWayMeetingRoomOption;
    }
}

//フリープレイPC

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.Start))]
class SystemConsoleStartPatch
{
    static bool Prefix(SystemConsole __instance)
    {
        if (__instance.FreeplayOnly && GeneralConfigurations.CurrentGameMode != CustomGameMode.FreePlay)
            UnityEngine.Object.Destroy(__instance.gameObject);

        return false;
    }
}


[HarmonyPatch(typeof(ArrowBehaviour), nameof(ArrowBehaviour.UpdatePosition))]
public static class ArrowUpdatePatch
{
    public static bool Prefix(ArrowBehaviour __instance)
    {
        //表示するのはUIカメラ
        Camera main = UnityHelper.FindCamera(LayerExpansion.GetUILayer())!;
        //距離を測るのは表示用のカメラ
        Camera worldCam = (NebulaGameManager.Instance?.WideCamera.IsShown ?? false) ? NebulaGameManager.Instance.WideCamera.Camera : Camera.main;

        Vector2 del = __instance.target - main.transform.position;

        float num = del.magnitude / (worldCam.orthographicSize * __instance.perc);
        if (__instance.image != null) __instance.image.enabled = (num > __instance.minDistanceToShowArrow);

        Vector2 vector = worldCam.WorldToViewportPoint(__instance.target);

        //カメラに合わせて見かけ上の位置に偽装させる
        var tempTarget = __instance.target;
        var diff = __instance.target - HudManager.Instance.transform.position;
        var pos = HudManager.Instance.transform.position + diff / (worldCam.orthographicSize / 3f);
        pos.z = tempTarget.z;
        __instance.target = pos;

        if (__instance.Between(vector.x, 0f, 1f) && __instance.Between(vector.y, 0f, 1f))
            __instance.CloseBehaviour(del, num);
        else
            __instance.DistancedBehaviour(vector, del, num, main);

        __instance.target = tempTarget;

        __instance.transform.LookAt2d(__instance.target);

        

        return false;
    }
}

[HarmonyPatch(typeof(Ladder), nameof(Ladder.MaxCoolDown), MethodType.Getter)]
class LadderCoolDownPatch
{
    static bool Prefix(Ladder __instance, out float __result)
    {
        __result = Math.Max(0.01f, GeneralConfigurations.LadderCoolDownOption.GetFloat());
        return false;
    }
}

[HarmonyPatch(typeof(Ladder), nameof(Ladder.SetDestinationCooldown))]
class LadderCoolDownUpdatePatch
{
    static bool Prefix(Ladder __instance)
    {
        float maxCoolDown = GeneralConfigurations.LadderCoolDownOption.GetFloat();
        __instance.Destination.CoolDown = maxCoolDown;
        __instance.CoolDown = maxCoolDown;
        return false;
    }
}

[HarmonyPatch(typeof(ZiplineConsole), nameof(ZiplineConsole.MaxCoolDown), MethodType.Getter)]
class ZiplineCoolDownPatch
{
    static bool Prefix(ZiplineConsole __instance, out float __result)
    {
        __result = Math.Max(0.01f, GeneralConfigurations.ZiplineCoolDownOption.GetFloat());
        return false;
    }
}

[HarmonyPatch(typeof(ZiplineConsole), nameof(ZiplineConsole.SetDestinationCooldown))]
class ZiplineCoolDownUpdatePatch
{
    static bool Prefix(ZiplineConsole __instance)
    {
        float maxCoolDown = GeneralConfigurations.ZiplineCoolDownOption.GetFloat();
        __instance.destination.CoolDown = maxCoolDown;
        __instance.CoolDown = maxCoolDown;
        return false;

    }
}
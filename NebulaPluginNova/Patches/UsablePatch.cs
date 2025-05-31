// 各種使用可能なオブジェクトに関するパッチ


using Virial;
using Virial.DI;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Patches;


[HarmonyPatch(typeof(Vent),nameof(Vent.CanUse))]
public static class VentCanUsePatch
{
    public static bool Prefix(Vent __instance, ref float __result,[HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
    {
        couldUse = true;
        
        float num = float.MaxValue;
        PlayerControl @object = pc.Object;
        GamePlayer? modInfo = NebulaGameManager.Instance?.GetPlayer(pc.PlayerId);

        //ダイブ中はベント使用不可
        if (modInfo?.IsDived ?? false)
        {
            canUse = couldUse = false;
            __result = num;
            return false;
        }

        if (@object.inVent && Vent.currentVent == __instance)
        {
            //既にベント内にいる場合
        }
        else {
            //ベント外にいる場合
            couldUse &= (modInfo?.Role.CanUseVent ?? false) || @object.inVent || @object.walkingToVent;
            if (modInfo?.Role.TaskType != Virial.Assignable.RoleTaskType.NoTask) couldUse &= !@object.MustCleanVent(__instance.Id);
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
        Color color = PlayerControl.LocalPlayer.GetModInfo()!.Unbox().Role.Role.Color.ToUnityColor();
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
        var p = pc.GetModInfo();
        if (p == null) return;
        GameOperatorManager.Instance?.Run(new PlayerVentEnterEvent(p, __instance));
        p.Role.VentDuration?.Start();
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
public static class ExitVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        pc.moveable = false;
        var p = pc.GetModInfo();
        if (p == null) return;
        GameOperatorManager.Instance?.Run(new PlayerVentExitEvent(p, __instance));
        p.Role.VentCoolDown?.Start();
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.Start))]
public static class VentStartPatch
{
    public static void Postfix(Vent __instance)
    {
        foreach (var b in __instance.Buttons)
        {
            b.spriteRenderer.gameObject.layer = LayerExpansion.GetUILayer();
            b.spriteRenderer.sortingOrder = 0;
        }
        var scale = __instance.transform.localScale;
        scale.z = 1f;
        __instance.transform.localScale = scale;
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

        var info = localPlayer.GetModInfo();

        if (isNotEnter)
        {
            localPlayer.MyPhysics.RpcExitVent(__instance.Id);
        }
        else if (!localPlayer.walkingToVent)
        {
            localPlayer.MyPhysics.RpcEnterVent(__instance.Id);
        }

        __instance.SetButtons(!isNotEnter && info!.Role.CanMoveInVent);
        return false;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.SetButtons))]
public static class VentUpdateArrowPatch1
{
    public static void Prefix(Vent __instance, [HarmonyArgument(0)] ref bool enabled)
    {
        if (GamePlayer.LocalPlayer != null)
        {
            if (!GamePlayer.LocalPlayer.Role.CanMoveInVent) enabled = false;
        }
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.UpdateArrows))]
public static class VentUpdateArrowPatch2
{
    public static void Postfix(Vent __instance)
    {
        if (GamePlayer.LocalPlayer != null)
        {
            if (!GamePlayer.LocalPlayer.Role.CanMoveInVent) __instance.SetButtons(false);
        }
    }
}

public static class CommonCanUsePatch
{
    public static bool CanUse(MonoBehaviour target, NetworkedPlayerInfo pc)
    {
        var info = NebulaGameManager.Instance?.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
        if (info == null) return true;

        if (info.AllAbilities.Any(a => a.BlockUsingUtility)) return false;

        //ダイブ中は使用不可
        if (info.IsDived) return false;

        return true;
    }

    public static bool Prefix(ref float __result, MonoBehaviour __instance, NetworkedPlayerInfo pc, out bool canUse, out bool couldUse)
    {
        canUse = couldUse = false;

        var info = NebulaGameManager.Instance?.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
        if (info == null) return true;

        if (!CanUse(__instance, pc))
        {
            __result = float.MaxValue;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Ladder), nameof(Ladder.CanUse))]
public static class LadderCanUsePatch
{
    public static bool Prefix(ref float __result, Ladder __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(ZiplineConsole), nameof(ZiplineConsole.CanUse))]
public static class ZiplineCanUsePatch
{
    public static bool Prefix(ref float __result, ZiplineConsole __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(PlatformConsole), nameof(PlatformConsole.CanUse))]
public static class PlatformCanUsePatch
{
    public static bool Prefix(ref float __result, PlatformConsole __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(DoorConsole), nameof(DoorConsole.CanUse))]
public static class DoorConsoleCanUsePatch
{
    public static bool Prefix(ref float __result, DoorConsole __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(OpenDoorConsole), nameof(OpenDoorConsole.CanUse))]
public static class OpenDoorConsoleCanUsePatch
{
    public static bool Prefix(ref float __result, OpenDoorConsole __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(DeconControl), nameof(DeconControl.CanUse))]
public static class DeconControlConsoleCanUsePatch
{
    public static bool Prefix(ref float __result, DeconControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(MapConsole), nameof(MapConsole.CanUse))]
public static class MapConsoleCanUsePatch
{
    public static bool Prefix(ref float __result, MapConsole __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse) => CommonCanUsePatch.Prefix(ref __result, __instance, pc, out canUse, out couldUse);
}

[HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
public static class ConsoleCanUsePatch
{
    public static bool Prefix(ref float __result, Console __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
    {
        canUse = couldUse = false;

        var info = NebulaGameManager.Instance?.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
        if (info == null) return true;

        if (!CommonCanUsePatch.CanUse(__instance, pc))
        {
            __result = float.MaxValue;
            return false;
        }

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

        if (info.Role.TaskType == Virial.Assignable.RoleTaskType.NoTask)
        {
            __result = float.MaxValue;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Console), nameof(Console.Use))]
public static class ConsoleUsePatch
{
    public static void Postfix(Console __instance)
    {
        if (Minigame.Instance && Minigame.Instance.Console && Minigame.Instance.Console.GetInstanceID() == __instance.GetInstanceID())
        {
            GameOperatorManager.Instance?.Run(new PlayerBeginMinigameByConsoleLocalEvent(GamePlayer.LocalPlayer!, __instance));
        }
    }
}

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.CanUse))]
public static class SystemConsoleCanUsePatch
{
    public static void Postfix(SystemConsole __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] ref bool canUse, [HarmonyArgument(2)] ref bool couldUse)
    {
        var info = NebulaGameManager.Instance?.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
        if (info == null) return;

        if (!CommonCanUsePatch.CanUse(__instance, pc))
        {
            canUse = false;
            couldUse = false;
        }

        //緊急会議コンソールの使用をブロック
        if (__instance.MinigamePrefab.TryCast<EmergencyMinigame>() && info.AllAssigned().Any(a => !a.CanCallEmergencyMeeting) && info.AllAbilities.Any(a => a.BlockCallingEmergencyMeeting))
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
        return !GeneralConfigurations.AirshipOneWayMeetingRoomOption.CurrentValue;
    }
}

[HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.InUse), MethodType.Getter)]
class CanUseMovingPlayformPatch
{
    static bool Prefix(MovingPlatformBehaviour __instance, out bool __result)
    {
        __result = false;
        if (AmongUsUtil.CurrentMapId != 4) return true;
        else {
            return !GeneralConfigurations.AirshipOneWayMeetingRoomOption.CurrentValue;
        }
    }
}

[HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.SetSide))]
class MovingPlatformBehaviourSetSidePatch
{
    static bool Prefix(MovingPlatformBehaviour __instance)
    {
        __instance.IsDirty = true;
        return !GeneralConfigurations.AirshipOneWayMeetingRoomOption.CurrentValue;
    }
}

//フリープレイPC

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.Start))]
class SystemConsoleStartPatch
{
    static bool Prefix(SystemConsole __instance)
    {
        if (__instance.FreeplayOnly && GeneralConfigurations.CurrentGameMode != GameModes.FreePlay)
            UnityEngine.Object.Destroy(__instance.gameObject);

        return false;
    }
}


[HarmonyPatch(typeof(ArrowBehaviour), nameof(ArrowBehaviour.UpdatePosition))]
public static class ArrowUpdatePatch
{
    public static bool Prefix(ArrowBehaviour __instance)
    {
        try
        {
            __instance.gameObject.layer = LayerExpansion.GetArrowLayer();
            __instance.image.sortingOrder = 10;

            //表示するのはUIカメラ
            Camera main = NebulaGameManager.Instance?.WideCamera.Camera ?? UnityHelper.FindCamera(LayerExpansion.GetUILayer())!;
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
            {
                Vector2 temp = worldCam.transform.position + (__instance.target - worldCam.transform.position) * (worldCam.orthographicSize / Camera.main.orthographicSize);
                __instance.transform.position = temp - del.normalized * 0.6f * (worldCam.orthographicSize / Camera.main.orthographicSize);
                __instance.transform.localScale = Vector3.one * Mathf.Clamp(num, 0f, 1f);
            }
            else
                __instance.DistancedBehaviour(vector, del, num, main);

            __instance.transform.localScale *= (worldCam.orthographicSize / Camera.main.orthographicSize);

            __instance.target = tempTarget;

            __instance.transform.LookAt2d(__instance.target);

            //Zの位置を調整
            var localPos = __instance.transform.localPosition;
            localPos.z = -100f;
            __instance.transform.localPosition = localPos;
        }catch(System.Exception e) { }
        return false;
    }
}

[HarmonyPatch(typeof(PingBehaviour), nameof(PingBehaviour.UpdatePosition))]
public static class PingUpdatePatch
{
    public static bool Prefix(PingBehaviour __instance) => ArrowUpdatePatch.Prefix(__instance);
}


[HarmonyPatch(typeof(Ladder), nameof(Ladder.MaxCoolDown), MethodType.Getter)]
class LadderCoolDownPatch

{
    //GeneralConfigurationsの初期化タイミングを調整するため、関数に抽出
    static float GetModCooldown() => GeneralConfigurations.LadderCoolDownOption;

    static bool Prefix(Ladder __instance, out float __result)
    {
        if (NebulaPreprocessorImpl.Finished)
        {
            __result = Math.Max(0.01f, GetModCooldown());
            return false;
        }

        __result = 0f;
        return true;
    }
}

[HarmonyPatch(typeof(Ladder), nameof(Ladder.SetDestinationCooldown))]
class LadderCoolDownUpdatePatch
{
    //GeneralConfigurationsの初期化タイミングを調整するため、関数に抽出
    static float GetModCooldown() => GeneralConfigurations.LadderCoolDownOption;

    static bool Prefix(Ladder __instance)
    {
        if (NebulaPreprocessorImpl.Finished)
        {
            float maxCoolDown = GeneralConfigurations.LadderCoolDownOption;
            __instance.Destination.CoolDown = maxCoolDown;
            __instance.CoolDown = maxCoolDown;
            return false;
        }
        else
        {
            return true;
        }
    }
}

[HarmonyPatch(typeof(ZiplineConsole), nameof(ZiplineConsole.MaxCoolDown), MethodType.Getter)]
class ZiplineCoolDownPatch
{
    static bool Prefix(ZiplineConsole __instance, out float __result)
    {
        __result = Math.Max(0.01f, GeneralConfigurations.ZiplineCoolDownOption);
        return false;
    }
}

[HarmonyPatch(typeof(ZiplineConsole), nameof(ZiplineConsole.SetDestinationCooldown))]
class ZiplineCoolDownUpdatePatch
{
    static bool Prefix(ZiplineConsole __instance)
    {
        float maxCoolDown = GeneralConfigurations.ZiplineCoolDownOption;
        __instance.destination.CoolDown = maxCoolDown;
        __instance.CoolDown = maxCoolDown;
        return false;

    }
}

[HarmonyPatch(typeof(ZiplineBehaviour), nameof(ZiplineBehaviour.CoAnimatePlayerJumpingOnToZipline))]
class ZiplineAnimateBeginPatch
{
    static void Postfix(ZiplineBehaviour __instance, [HarmonyArgument(0)]PlayerControl player,[HarmonyArgument(1)]bool fromTop, [HarmonyArgument(2)]HandZiplinePoolable hand)
    {
        player.GetModInfo()?.Unbox().AddPlayerColorRenderers(hand.handRenderer);

        try
        {
            player.GetModInfo()!.Unbox().GoalPos = fromTop ? __instance.landingPositionBottom.position : __instance.landingPositionTop.position;
        }
        catch
        {
            Debug.Log($"Skipped presetting goal position on use ZiplineBehaviour. (for {__instance.name})");
        }
    }
}

[HarmonyPatch(typeof(ZiplineBehaviour), nameof(ZiplineBehaviour.CoAlightPlayerFromZipline))]
class ZiplineAnimateEndPatch
{
    static void Postfix(ZiplineBehaviour __instance, ref Il2CppSystem.Collections.IEnumerator __result, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(3)] HandZiplinePoolable hand)
    {
        var orig = __result;
        __result = Effects.Sequence(orig, ManagedEffects.Action(() =>
        {
            player.GetModInfo()?.Unbox().RemovePlayerColorRenderer(hand.handRenderer);
        }).WrapToIl2Cpp());

        //死体生成のための目標地点をリセットする。
        player.GetModInfo()?.Unbox().ResetDeadBodyGoalPos();
    }
}
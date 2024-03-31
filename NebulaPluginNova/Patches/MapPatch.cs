using Nebula.Behaviour;
using Rewired.Utils.Platforms.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Patches;

[HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.OnEnable))]
public static class OpenMapCountOverlayPatch
{

    static void Prefix(MapCountOverlay __instance)
    {
        __instance.InitializeModOption();

        var timer = NebulaGameManager.Instance?.ConsoleRestriction?.ShowTimerIfNecessary(ConsoleRestriction.ConsoleType.Admin, __instance.transform, new Vector3(4.8f, 2f, -50f));
        if (timer != null) timer.transform.localScale = Vector3.one / __instance.transform.localScale.x;
    }
}


[HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.Update))]
public static class CountOverlayUpdatePatch
{
    static TMPro.TextMeshPro notAvailableText = null!;

    static bool Prefix(MapCountOverlay __instance)
    {
        if (!ConsoleTimer.IsOpenedByAvailableWay())
        {
            __instance.BackgroundColor.SetColor(Palette.DisabledGrey);
            if (!notAvailableText) {
                notAvailableText = GameObject.Instantiate(__instance.SabotageText, __instance.SabotageText.transform.parent);
                notAvailableText.text = Language.Translate("console.notAvailable");
                notAvailableText.GetComponent<AlphaBlink>().enabled = true;
            }
            notAvailableText.gameObject.SetActive(true);
            __instance.SabotageText.gameObject.SetActive(false);
            foreach (var counterArea in __instance.CountAreas) counterArea.UpdateCount(0);

            return false;
        }

        if(notAvailableText) notAvailableText.gameObject.SetActive(false);

        __instance.timer += Time.deltaTime;
        if (__instance.timer < 0.1f) return false;
        
        __instance.timer = 0f;
        
        if (!__instance.isSab && MapBehaviourExtension.AffectedByCommSab && PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer))
        {
            __instance.isSab = true;
            __instance.BackgroundColor.SetColor(Palette.DisabledGrey);
            __instance.SabotageText.gameObject.SetActive(true);
        }
        if (__instance.isSab && (!MapBehaviourExtension.AffectedByCommSab || !PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer)))
        {
            __instance.isSab = false;
            __instance.BackgroundColor.SetColor(MapBehaviourExtension.MapColor ?? Color.green);
            __instance.SabotageText.gameObject.SetActive(false);
        }

        if (__instance.isSab)
        {
            foreach (var counterArea in __instance.CountAreas) counterArea.UpdateCount(0);
            return false;
        }

        int mask = 0;
        bool AlreadyAdded(byte playerId) => (mask & (1 << playerId)) != 0;
        void AddToMask(byte playerId) => mask |= (1 << playerId);

        FakeAdmin admin = MapBehaviourExtension.AffectedByFakeAdmin ? FakeInformation.Instance!.CurrentAdmin : FakeInformation.AdminFromActuals;

        foreach (var counterArea in __instance.CountAreas)
        {
            if (ShipStatus.Instance.FastRooms.TryGetValue(counterArea.RoomType, out var plainShipRoom) && plainShipRoom.roomArea)
            {
                int num = plainShipRoom.roomArea.OverlapCollider(__instance.filter, __instance.buffer);
                int counter = 0;
                int deadBodies = 0, impostors = 0;

                if (MeetingHud.Instance)
                {
                    //会議中のアドミン (PreMeetingPointを参照する)
                    foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                    {
                        if (!p.IsDead && plainShipRoom.roomArea.OverlapPoint(p.PreMeetingPoint) && !AlreadyAdded(p.PlayerId)) {
                            AddToMask(p.PlayerId);
                            counter++;
                            if (p.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && MapBehaviourExtension.CanIdentifyImpostors) impostors++;
                        }
                    }
                }
                else
                {
                    //タスクターン中のアドミン
                    foreach(var p in admin.Players)
                    {
                        if (MapBehaviourExtension.AffectedByFakeAdmin && (NebulaGameManager.Instance?.GetModPlayerInfo(p.playerId)?.HasAttribute(PlayerAttributes.Isolation) ?? false)) continue;

                        if (AlreadyAdded(p.playerId)) continue;

                        if (plainShipRoom.roomArea.OverlapPoint(p.position))
                        {
                            counter++;

                            if (p.isDead && MapBehaviourExtension.CanIdentifyDeadBodies) deadBodies++;
                            if (p.isImpostor && MapBehaviourExtension.CanIdentifyImpostors) impostors++;

                            AddToMask(p.playerId);
                        }
                    }
                }

                counterArea.UpdateCount(counter, impostors, deadBodies);
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowNormalMap))]
public static class OpenNormalMapPatch
{

    static void Postfix(MapCountOverlay __instance)
    {
        PlayerControl.LocalPlayer.GetModInfo()?.AssignableAction(r=>r.OnOpenNormalMap());
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
public static class OpenSabotageMapPatch
{

    static void Postfix(MapCountOverlay __instance)
    {
        PlayerControl.LocalPlayer.GetModInfo()?.AssignableAction(r => r.OnOpenSabotageMap());
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowCountOverlay))]
public static class OpenAdminMapPatch
{

    static void Postfix(MapCountOverlay __instance)
    {
        PlayerControl.LocalPlayer.GetModInfo()?.AssignableAction(r => r.OnOpenAdminMap());
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Awake))]
public static class InitMapPatch
{
    static void Postfix(MapBehaviour __instance)
    {
        PlayerControl.LocalPlayer.GetModInfo()?.AssignableAction(r => r.OnMapInstantiated());
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.GenericShow))]
static class MapBehaviourGenericShowPatch
{
    static void Postfix(MapBehaviour __instance)
    {
        __instance.transform.localPosition = new Vector3(0, 0, -30f);
    }
}
using Nebula.Behavior;
using Nebula.Map;
using System.Reflection.Metadata;
using Virial.Events.Game.Minimap;
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

        HashSet<int> mask = [];
        bool AlreadyAdded(int id) => mask.Contains(id);
        void AddToMask(int id) => mask.Add(id);

        FakeAdmin admin = MapBehaviourExtension.AffectedByFakeAdmin ? FakeInformation.Instance!.CurrentAdmin : FakeInformation.AdminFromActuals;
        var roomFlags = MapData.GetCurrentMapData().AdminRooms;
        foreach (var counterArea in __instance.CountAreas)
        {
            var index = roomFlags.FindIndex(e => e.room == counterArea.RoomType);
            if (index != -1 && (MapBehaviourExtension.RoomFlag & (0b10 << index)) == 0)
            {
                counterArea.UpdateCount(0, 0, 0);
            }
            else
            {
                if (ShipStatus.Instance.FastRooms.TryGetValue(counterArea.RoomType, out var plainShipRoom) && plainShipRoom.roomArea)
                {
                    int counter = 0;
                    int deadBodies = 0, impostors = 0;

                    if (MeetingHud.Instance)
                    {
                        //会議中のアドミン (PreMeetingPointを参照する)
                        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                        {
                            if (!p.IsDead && plainShipRoom.roomArea.OverlapPoint(p.Unbox().PreMeetingPoint) && !AlreadyAdded(p.PlayerId))
                            {
                                AddToMask(p.PlayerId);
                                counter++;
                                if (p.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && MapBehaviourExtension.CanIdentifyImpostors) impostors++;
                            }
                        }
                    }
                    else
                    {
                        //タスクターン中のアドミン
                        for (int i = 0;i< admin.Players.Length;i++)
                        {
                            var p = admin.Players[i];

                            if (p.isDead && !MapBehaviourExtension.ShowDeadBodies) continue;
                            if (MapBehaviourExtension.AffectedByFakeAdmin && (NebulaGameManager.Instance?.GetPlayer(p.playerId)?.HasAttribute(PlayerAttributes.Isolation) ?? false)) continue;

                            if (AlreadyAdded(i)) continue;

                            if (plainShipRoom.roomArea.OverlapPoint(p.position))
                            {
                                counter++;

                                if (p.isDead && MapBehaviourExtension.CanIdentifyDeadBodies) deadBodies++;
                                if (p.isImpostor && MapBehaviourExtension.CanIdentifyImpostors) impostors++;

                                AddToMask(i);
                            }
                        }
                    }

                    counterArea.UpdateCount(counter, impostors, deadBodies);
                }
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowNormalMap))]
public static class OpenNormalMapPatch
{
    //CanMoveのチェックを排除
    static bool Prefix(MapBehaviour __instance)
    {
        if (__instance.IsOpen)
        {
            __instance.Close();
            return false;
        }
        
        PlayerControl.LocalPlayer.SetPlayerMaterialColors(__instance.HerePoint);
        __instance.GenericShow();
        __instance.ColorControl.SetColor(new Color(0.05f, 0.2f, 1f, 1f));
        __instance.taskOverlay.Show();
        DestroyableSingleton<HudManager>.Instance.SetHudActive(false);

        GameOperatorManager.Instance?.Run(new MapOpenNormalEvent(), true);
        return false;
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
public static class OpenSabotageMapPatch
{
    //CanMoveのチェックを排除
    static bool Prefix(MapBehaviour __instance)
    {
        if (__instance.IsOpen)
        {
            __instance.Close();
            return false;
        }
        if (__instance.specialInputHandler != null) __instance.specialInputHandler.disableVirtualCursor = true;
        
        PlayerControl.LocalPlayer.SetPlayerMaterialColors(__instance.HerePoint);
        __instance.GenericShow();
        __instance.ColorControl.SetColor(Palette.ImpostorRed);
        __instance.infectedOverlay.gameObject.SetActive(true);
        __instance.taskOverlay.Show();
        DestroyableSingleton<HudManager>.Instance.SetHudActive(false);
        ConsoleJoystick.SetMode_Sabotage();

        GameOperatorManager.Instance?.Run(new MapOpenSabotageEvent(), true);
        return false;
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowCountOverlay))]
public static class OpenAdminMapPatch
{
    //CanMoveのチェックは元より入ってない

    static void Postfix(MapCountOverlay __instance)
    {
        GameOperatorManager.Instance?.Run(new MapOpenAdminEvent(), true);
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Awake))]
public static class InitMapPatch
{
    static void Postfix(MapBehaviour __instance)
    {
        GameOperatorManager.Instance?.Run(new MapInstantiateEvent());
        __instance.fadedBackground.transform.localScale *= 2f;

        MapBehaviourExtension.SetUpAsMinimapContent(__instance.transform.FindChild(c => c.name.StartsWith("RoomNames")).gameObject);
        MapBehaviourExtension.SetUpAsMinimapContent(__instance.HerePoint.gameObject);
        MapBehaviourExtension.SetUpAsMinimapContent(__instance.infectedOverlay.gameObject);
        __instance.countOverlay.CountAreas.FirstOrDefault()?.pool.Prefab.gameObject.GetOrAddComponent<MinimapScaler>();
        __instance.taskOverlay.icons.Prefab.gameObject.GetOrAddComponent<MinimapScaler>();
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.GenericShow))]
static class MapBehaviourGenericShowPatch
{
    static void Postfix(MapBehaviour __instance)
    {
        __instance.transform.localPosition = new Vector3(0, 0, -50f);
        __instance.ColorControl.GetComponent<SpriteRenderer>().sprite = NebulaAsset.GetMapSprite(AmongUsUtil.CurrentMapId, 0xFFFFFF); //ShipStatus.Instance.MapPrefab.ColorControl.GetComponent<SpriteRenderer>().sprite;
        MapBehaviourExtension.UpdateScale(__instance);
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
class MapBehaviourShowNormalMapPatch
{
    static bool Prefix(MapBehaviour __instance, [HarmonyArgument(0)] MapOptions opts)
    {
        if (__instance.IsOpen)
        {
            __instance.Close();
            return false;
        }

        if (ExileController.Instance || Minigame.Instance) return false;


        if (!PlayerControl.LocalPlayer.CanMove && !MeetingHud.Instance) {
            //会議中でなく動けないとき

            int val = GeneralConfigurations.CanOpenMapWhileUsingUtilityOption.GetValue();
            if (val == 2 || (val == 1 && !(GamePlayer.LocalPlayer?.IsTrueCrewmate ?? true))) {
                //何れかのユーティリティを使用してなければ開けない
                if(!PlayerControl.LocalPlayer.inVent && !PlayerControl.LocalPlayer.inMovingPlat && !PlayerControl.LocalPlayer.walkingToVent && !PlayerControl.LocalPlayer.onLadder) return false; 
            }
            else
            {
                return false;
            }
            
        }

        switch (opts.Mode)
        {
            case MapOptions.Modes.Normal:
                __instance.ShowNormalMap();
                return false;
            case MapOptions.Modes.CountOverlay:
                __instance.ShowCountOverlay(opts.AllowMovementWhileMapOpen, opts.ShowLivePlayerPosition, opts.IncludeDeadBodies);
                return false;
            case MapOptions.Modes.Sabotage:
                __instance.ShowSabotageMap();
                return false;
        }
        return false;
    }

}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.FixedUpdate))]
public static class MapUpdatePatch
{

    //向きの調整
    static void Postfix(MapBehaviour __instance)
    {
        MapBehaviourExtension.UpdateScale(__instance);
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Close))]
public static class MapClosePatch
{
    static void Postfix(MapBehaviour __instance)
    {
        GameOperatorManager.Instance?.Run(new MapCloseEvent());
    }
}

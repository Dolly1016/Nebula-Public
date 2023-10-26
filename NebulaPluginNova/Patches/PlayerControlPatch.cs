using AmongUs.Data.Player;
using Epic.OnlineServices.Presence;
using HarmonyLib;
using Nebula.Behaviour;
using Nebula.Configuration;
using Nebula.Game;
using Nebula.Modules;
using PowerTools;
using static UnityEngine.RemoteConfigSettingsHelper;

namespace Nebula.Patches;


[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
public static class PlayerStartPatch
{
    static void Postfix(PlayerControl __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = Effects.Sequence(new Il2CppSystem.Collections.IEnumerator[] {
            __result,
            Effects.Action((Il2CppSystem.Action)(()=>{
                if(PlayerControl.LocalPlayer)DynamicPalette.RpcShareColor.Invoke(new DynamicPalette.ShareColorMessage() { playerId = PlayerControl.LocalPlayer.PlayerId }.ReflectMyColor());
            }))
            }.ToArray());
    }
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.Color), MethodType.Setter)]
public static class PlayerGetColorPatch
{
    static bool Prefix(PlayerCustomizationData __instance)
    {
        __instance.colorID = 15;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.OnLoadComplete))]
public static class PlayerSetColorPatch
{
    static void Postfix(PlayerData __instance)
    {
        __instance.customization.colorID = 15;
    }
}


[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckColor))]
public static class PlayerColorPatch
{
    static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] ref byte bodyColor)
    {
        bodyColor = __instance.PlayerId;
    }
}


[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class PlayerUpdatePatch
{
    static IEnumerable<SpriteRenderer> AllHighlightable()
    {
        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) yield return p.cosmetics.currentBodySprite.BodySprite;
        foreach (var d in Helpers.AllDeadBodies()) foreach (var r in d.bodyRenderers) yield return r;
    }

    static void Prefix(PlayerControl __instance)
    {
        if (__instance.AmOwner)
        {
            foreach(var r in AllHighlightable()) r.material.SetFloat("_Outline", 0f);
        }
    }

    static void Postfix(PlayerControl __instance)
    {
        if (NebulaGameManager.Instance == null) return;
        if (NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted) return;

        NebulaGameManager.Instance.GetModPlayerInfo(__instance.PlayerId)?.Update();

        if (__instance.AmOwner) NebulaGameManager.Instance.OnFixedUpdate();

        if(__instance.cosmetics.transform.localScale.z < 100f)
        {
            var scale = __instance.cosmetics.transform.localScale;
            scale.z = 100f;
            __instance.cosmetics.transform.localScale = scale;
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetHatAndVisorAlpha))]
public class PlayerControlSetAlphaPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (NebulaGameManager.Instance == null) return;
        if (NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted) return;

        NebulaGameManager.Instance.GetModPlayerInfo(__instance.PlayerId)?.UpdateVisibility(false);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
public static class PlayerCompleteTaskPatch
{
    static void Postfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner) return;

        __instance.GetModInfo()?.Tasks.OnCompleteTask();
        __instance.GetModInfo()?.RoleAction((r)=>r.OnTaskCompleteLocal());
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
public static class PlayerStartMeetingPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo info)
    {
        TranslatableTag tag = info == null ? EventDetail.EmergencyButton : EventDetail.Report;

        if (info != null)
        {
            var targetInfo = Helpers.GetPlayer(info.PlayerId)!.GetModInfo();

            //ベイトレポートチェック
            if (targetInfo?.Role.Role is Roles.Crewmate.Bait && ((targetInfo.MyKiller?.PlayerId ?? byte.MaxValue) == __instance.PlayerId) && (targetInfo.DeathTimeStamp.HasValue && NebulaGameManager.Instance!.CurrentTime - targetInfo.DeathTimeStamp.Value < 3f))
                tag = EventDetail.BaitReport;
        }

        NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(
            info == null ? GameStatistics.EventVariation.EmergencyButton : GameStatistics.EventVariation.Report, __instance.PlayerId,
            info == null ? 0 : (1 << info.PlayerId))
        { RelatedTag = tag });
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.Initialize))]
class OverlayKillAnimationPatch
{
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] GameData.PlayerInfo kInfo, [HarmonyArgument(1)] GameData.PlayerInfo vInfo)
    {
        if (__instance.killerParts)
        {
            GameData.PlayerOutfit? currentOutfit = NebulaGameManager.Instance?.GetModPlayerInfo(kInfo.PlayerId)?.CurrentOutfit;
            if (currentOutfit != null)
            {
                __instance.killerParts.SetBodyType(PlayerBodyTypes.Normal);
                __instance.killerParts.UpdateFromPlayerOutfit(currentOutfit, PlayerMaterial.MaskType.None, false, false);
                __instance.killerParts.ToggleName(false);
                __instance.LoadKillerSkin(currentOutfit);
                __instance.LoadKillerPet(currentOutfit);
            }
        }
        if (vInfo != null && __instance.victimParts)
        {
            GameData.PlayerOutfit? defaultOutfit = NebulaGameManager.Instance?.GetModPlayerInfo(vInfo.PlayerId)?.DefaultOutfit;
            if (defaultOutfit != null)
            {
                __instance.victimHat = defaultOutfit.HatId;
                __instance.killerParts.SetBodyType(PlayerBodyTypes.Normal);
                __instance.victimParts.UpdateFromPlayerOutfit(defaultOutfit, PlayerMaterial.MaskType.None, false, false);
                __instance.victimParts.ToggleName(false);
                __instance.LoadVictimSkin(defaultOutfit);
                __instance.LoadVictimPet(defaultOutfit);
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetTasks))]
class SetTaskPAtch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] Il2CppSystem.Collections.Generic.List<GameData.TaskInfo> tasks)
    {
        if (!__instance.AmOwner) return true;

        var tasksList = tasks.ToArray().ToList();
        int num = tasksList.Count;
        var info = __instance.GetModInfo();
        info?.RoleAction((r)=>r.OnSetTaskLocal(ref tasksList));
        if (num != tasksList.Count) info?.Tasks.ReplaceTasks(tasksList.Count);

        __instance.StartCoroutine(CoSetTasks().WrapToIl2Cpp());

        IEnumerator CoSetTasks()
        {
            while (!ShipStatus.Instance) yield return null;

            HudManager.Instance.TaskStuff.SetActive(true);

            for (int i = 0; i < __instance.myTasks.Count; i++) GameObject.Destroy(__instance.myTasks[i].gameObject);
            __instance.myTasks.Clear();

            __instance.Data.Role.SpawnTaskHeader(__instance);
            for (int i = 0; i < tasksList.Count; i++)
            {
                GameData.TaskInfo taskInfo = tasksList[i];
                NormalPlayerTask normalPlayerTask = GameObject.Instantiate<NormalPlayerTask>(ShipStatus.Instance.GetTaskById(taskInfo.TypeId), __instance.transform);
                normalPlayerTask.Id = taskInfo.Id;
                normalPlayerTask.Owner = __instance;
                normalPlayerTask.Initialize();
                __instance.myTasks.Add(normalPlayerTask);
            }
            yield break;
        }

        return false;
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.HandleDisconnect), typeof(PlayerControl), typeof(DisconnectReasons))]
class PlayerDisconnectPatch
{
    public static void Postfix(GameData __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] DisconnectReasons reason)
    {
        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.NotStarted) return;

        player.GetModInfo()!.IsDisconnected = true;

        NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Disconnect, player.PlayerId, 0){ RelatedTag = EventDetail.Disconnect }) ;
    }
}


[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoSpawnPlayer))]
public class PlayerJoinedPatch
{
    public static void Postfix()
    {
        if (AmongUsClient.Instance.AmHost) NebulaConfigEntryManager.ShareAll();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter)]
class PlayerCanMovePatch
{
    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (__instance != PlayerControl.LocalPlayer) return;

        __result &= !TextField.AnyoneValid && HudManager.Instance.PlayerCam.Target == PlayerControl.LocalPlayer;
    }
}

//指定位置への移動
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.WalkPlayerTo))]
class WalkPatch
{
    public static void Postfix(PlayerPhysics __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var info = __instance.myPlayer.GetModInfo();
        if (info == null) return;

        __result = Effects.Sequence(
            Effects.Action((Il2CppSystem.Action)(() =>
            {
                float speed = info.CalcSpeed();
                if (speed < 0f) speed *= -1f;
                __instance.Speed = speed;
            })),
            __result,
            Effects.Action((Il2CppSystem.Action)(() =>
            {
                __instance.Speed = info.CalcSpeed();
            }))
            );
    }
}

[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation), typeof(GameData.PlayerInfo), typeof(GameData.PlayerInfo))]
public static class KillOverlayPatch
{
    public static bool Prefix(KillOverlay __instance, GameData.PlayerInfo killer, GameData.PlayerInfo victim)
    {
        if (killer.PlayerId == victim.PlayerId)
        {
            __instance.ShowKillAnimation(__instance.KillAnims[3], killer, victim);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.Initialize))]
public static class OverlayKillAnimationInitializePatch
{
    public static void Postfix(OverlayKillAnimation __instance, GameData.PlayerInfo kInfo, GameData.PlayerInfo vInfo)
    {
        if (kInfo.PlayerId == vInfo.PlayerId)
        {
            __instance.transform.GetChild(0).gameObject.SetActive(false);
            __instance.transform.GetChild(2).gameObject.SetActive(false);

            float x = 0.6f;
            var victim = __instance.transform.GetChild(1);
            victim.localPosition = new(x, 0f, 0f);
            var pet = __instance.transform.GetChild(3);
            pet.localPosition = new(x, -0.37f, 0f);

            IEnumerator CoMoveLeft()
            {
                yield return new WaitForSeconds(0.44f);
                while(x > -1.8f)
                {
                    if (!__instance) yield break;
                    x -= (x - (-1.8f)) * Time.deltaTime * 5.2f;
                    victim!.localPosition = new(x, 0f, 0f);
                    pet!.localPosition = new(x, -0.37f, 0f);
                    yield return null;
                }
            }
            NebulaManager.Instance.StartCoroutine(CoMoveLeft().WrapToIl2Cpp());
        }
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.LoadVictimSkin))]
public static class LoadVictimSkinPatch
{
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] GameData.PlayerOutfit victimOutfit)
    {
        var script = __instance.victimParts.gameObject.AddComponent<ScriptBehaviour>();
        SkinViewData skin = ShipStatus.Instance.CosmeticsCache.GetSkin(victimOutfit.SkinId);
        SpriteAnim skinSpriteAnim = __instance.victimParts.GetSkinSpriteAnim();

        script.ActiveHandler += () =>
        {
            
            switch (__instance.KillType)
            {
                case KillAnimType.Stab:
                    skinSpriteAnim.Play(skin.KillStabVictim, 1f);
                    break;
                case KillAnimType.Tongue:
                    skinSpriteAnim.Play(skin.KillTongueVictim, 1f);
                    break;
                case KillAnimType.Shoot:
                    skinSpriteAnim.Play(skin.KillShootVictim, 1f);
                    break;
                case KillAnimType.Neck:
                    skinSpriteAnim.Play(skin.KillNeckVictim, 1f);
                    break;
                case KillAnimType.RHM:
                    skinSpriteAnim.Play(skin.KillRHMVictim, 1f);
                    break;
                default:
                    break;
            }
        };
        if (script.gameObject.active) script.OnEnable();
        return false;
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.LoadKillerSkin))]
public static class LoadKillerSkinPatch
{
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] GameData.PlayerOutfit killerOutfit)
    {
        var script = __instance.killerParts.gameObject.AddComponent<ScriptBehaviour>();
        SkinViewData skin = ShipStatus.Instance.CosmeticsCache.GetSkin(killerOutfit.SkinId);
        SpriteAnim skinSpriteAnim = __instance.killerParts.GetSkinSpriteAnim();

        script.ActiveHandler += () =>
        {

            switch (__instance.KillType)
            {
                case KillAnimType.Stab:
                    skinSpriteAnim.Play(skin.KillStabImpostor, 1f);
                    break;
                case KillAnimType.Tongue:
                    skinSpriteAnim.Play(skin.KillTongueImpostor, 1f);
                    break;
                case KillAnimType.Shoot:
                    skinSpriteAnim.Play(skin.KillShootImpostor, 1f);
                    break;
                case KillAnimType.Neck:
                    skinSpriteAnim.Play(skin.KillNeckImpostor, 1f);
                    break;
                default:
                    break;
            }
        };
        if (script.gameObject.active) script.OnEnable();
        return false;
    }
}


//移動後の位置に死体が発生するようにする

[HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.UsePlatform))]
public static class UsePlatformPatch
{
    public static void Postfix(MovingPlatformBehaviour __instance, [HarmonyArgument(0)]PlayerControl target)
    {
        try
        {
            target.GetModInfo()!.GoalPos = __instance.transform.parent.TransformPoint((!__instance.IsLeft) ? __instance.LeftUsePosition : __instance.RightUsePosition);
        }
        catch {
            Debug.Log($"Skipped presetting goal position on use MovingPlatform. (for {target.name})");
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ClimbLadder))]
public static class UseLadderPatch
{
    public static void Postfix(PlayerPhysics __instance, [HarmonyArgument(0)] Ladder source)
    {
        try
        {
            Vector2 pos = source.Destination.transform.position;
            if (source.Destination.IsTop) pos += new Vector2(0f, 1.2f);
            __instance.myPlayer.GetModInfo()!.GoalPos = pos;
        }
        catch
        {
            Debug.Log($"Skipped presetting goal position on climbing Ladder. (for {__instance.name})");
        }
    }
}

[HarmonyPatch(typeof(ZiplineBehaviour), nameof(ZiplineBehaviour.Use),typeof(PlayerControl),typeof(bool))]
public static class UseZiplinePatch
{
    public static void Postfix(ZiplineBehaviour __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] bool fromTop)
    {
        try
        {
            player.GetModInfo()!.GoalPos = fromTop ? __instance.landingPositionBottom.position : __instance.landingPositionTop.position;
        }
        catch
        {
            Debug.Log($"Skipped presetting goal position on use ZiplineBehaviour. (for {__instance.name})");
        }
    }
}


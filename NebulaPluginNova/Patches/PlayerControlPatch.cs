using AmongUs.Data.Player;
using Hazel;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using PowerTools;
using UnityEngine;
using Virial.Events.Player;
using Virial.Game;
using static DynamicSound;
using static Il2CppSystem.Globalization.CultureInfo;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Patches;


[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
public static class PlayerStartPatch
{
    static void Postfix(PlayerControl __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = Effects.Sequence(
            __result,
            Effects.Action((Il2CppSystem.Action)(()=>
            {
                __instance.SetColor(__instance.PlayerId);
                if (PlayerControl.LocalPlayer) DynamicPalette.RpcShareMyColor();
                if (__instance.AmOwner)
                {
                    __instance.lightSource.lightChild.layer = LayerExpansion.GetVanillaShadowLightLayer();
                }

                if (AmongUsClient.Instance.AmHost) ConfigurationValues.ShareAll();
            }))
            );

        //人数が多いと近くのコンソールも追跡できなくなるので、上限を緩和
        __instance.hitBuffer = new Collider2D[120];
        
        /*
        IEnumerator CoShowPosition()
        {

            var renderer = UnityHelper.CreateObject<SpriteRenderer>("PlayerPos", null, Vector3.zero, LayerExpansion.GetDefaultLayer());
            renderer.sprite = image.GetSprite();
            while (renderer && __instance)
            {
                if (__instance.NetTransform.incomingPosQueue.Count > 0) {
                    renderer.enabled = true;
                    renderer.transform.position = __instance.NetTransform.incomingPosQueue.ToArray().Last();
                    renderer.transform.SetWorldZ(-10f);
                }
                else
                {
                    renderer.enabled = false;
                }
                yield return null;
            }
            if(renderer) GameObject.Destroy(renderer);
        }
        NebulaManager.Instance.StartCoroutine(CoShowPosition().WrapToIl2Cpp());
        */
    }
    static private Image image = SpriteLoader.FromResource("Nebula.Resources.WhiteCircle.png", 100f);
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.Color), MethodType.Setter)]
public static class PlayerGetColorPatch
{
    static bool Prefix(PlayerCustomizationData __instance)
    {
        __instance.colorID = NebulaPlayerTab.PreviewColorId;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckColor))]
public static class PlayerCheckColorPatch
{
    static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] ref byte bodyColor)
    {
        bodyColor = __instance.PlayerId;
        return true;
    }
}

[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.OnLoadComplete))]
public static class PlayerSetColorPatch
{
    static void Postfix(PlayerData __instance)
    {
        __instance.customization.colorID = NebulaPlayerTab.PreviewColorId;
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
        if (ShipStatus.Instance)
        {
            foreach (var v in ShipStatus.Instance.AllVents) yield return v.myRend;
            foreach (var c in ShipStatus.Instance.AllConsoles) if(c.Image) yield return c.Image;
        }
        foreach (var cc in ModSingleton<CustomConsoleManager>.Instance.AllCustomConsoles) yield return cc.Renderer;
    }

    static private float lastKillTimer = 0f;
    static void Prefix(PlayerControl __instance)
    {
        lastKillTimer = __instance.killTimer;

        if (__instance.AmOwner)
        {
            foreach(var r in AllHighlightable()) r.material.SetFloat("_Outline", 0f);
        }
    }

    
    static void Postfix(PlayerControl __instance)
    {
        if (NebulaGameManager.Instance == null) return;
        if (__instance.AmOwner) NebulaGameManager.Instance.OnFixedAlwaysUpdate();

        if (NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted)
        {
            bool showVanillaColor = ClientOption.AllOptions[ClientOption.ClientOptionType.ShowVanillaColor].Value == 1;
            try
            {
                if (showVanillaColor) __instance.cosmetics.nameText.text = __instance.Data.PlayerName + " ■".Color(DynamicPalette.VanillaColorsPalette[__instance.PlayerId]);
                else __instance.cosmetics.nameText.text = __instance.Data.PlayerName;

                __instance.cosmetics.nameText.transform.parent.gameObject.SetActive(!ModSingleton<ShowUp>.Instance.AnyoneShowedUp);
            }
            catch { }
            return;
        }

        NebulaGameManager.Instance.GetPlayer(__instance.PlayerId)?.Unbox().Update();

        if (__instance.AmOwner)
        {
            NebulaGameManager.Instance.OnFixedUpdate();

            //ペット・レポートボタンの使用可否
            if (HudManager.InstanceExists && GamePlayer.LocalPlayer.IsDived)
            {
                HudManager.Instance.ReportButton.SetDisabled();
                HudManager.Instance.PetButton.SetDisabled();
            }

            if (__instance.inVent && Vent.currentVent)
            {
                Vector2 vector = Vent.currentVent.transform.position;
                vector -= __instance.Collider.offset;

                if (__instance.MyPhysics.body.transform.position.Distance(vector) > 0.001f) __instance.NetTransform.RpcSnapTo(vector);
            }

            //キルボタンのクールダウン進行
            {
                var data = __instance.Data;
                bool flag = data.Role.CanUseKillButton && !data.IsDead;
                if ((__instance.IsKillTimerEnabled || __instance.ForceKillTimerContinue) && flag)
                {
                    float deltaTime = Time.fixedDeltaTime;
                    float coeff = GamePlayer.LocalPlayer?.Unbox().CalcAttributeVal(PlayerAttributes.CooldownSpeed, true) ?? 1f;
                    deltaTime *= coeff;
                    __instance.SetKillTimer(lastKillTimer - deltaTime);
                }
            }
        }

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

        NebulaGameManager.Instance.GetPlayer(__instance.PlayerId)?.Unbox().UpdateVisibility(false);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.AddSystemTask))]
public static class PlayerAddSystemTaskPatch
{
    static void Postfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner) return;

        var task = __instance.myTasks[__instance.myTasks.Count - 1];
        GameOperatorManager.Instance?.Run(new PlayerSabotageTaskAddLocalEvent(GamePlayer.LocalPlayer, task));
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveTask))]
public static class PlayerRemoveTaskPatch
{
    static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (__instance.AmOwner)
        {
            GameOperatorManager.Instance?.Run(new PlayerTaskRemoveLocalEvent(GamePlayer.LocalPlayer, task));
            Debug.Log("Remove Task");
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
public static class PlayerCompleteTaskPatch
{
    static void Postfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner) return;

        __instance.GetModInfo()?.Tasks.Unbox().OnCompleteTask();
        GameOperatorManager.Instance?.Run(new PlayerTaskCompleteLocalEvent(__instance.GetModInfo()!));
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
public static class PlayerStartMeetingPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo info)
    {
        TranslatableTag tag = info == null ? EventDetail.EmergencyButton : EventDetail.Report;

        if (info != null)
        {
            var targetInfo = Helpers.GetPlayer(info.PlayerId)!.GetModInfo();

            //ベイトレポートチェック
            if (targetInfo?.Role.Role is Roles.Crewmate.Bait && ((targetInfo.MyKiller?.PlayerId ?? byte.MaxValue) == __instance.PlayerId) && (targetInfo.Unbox().DeathTimeStamp.HasValue && NebulaGameManager.Instance!.CurrentTime - targetInfo.Unbox().DeathTimeStamp!.Value < 3f))
                tag = EventDetail.BaitReport;
        }

        NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(
            info == null ? GameStatistics.EventVariation.EmergencyButton : GameStatistics.EventVariation.Report, __instance.PlayerId,
            info == null ? 0 : (1 << info.PlayerId))
        { RelatedTag = tag });
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CurrentOutfit), MethodType.Getter)]
class CurrentOutfitPatch
{
    public static bool Prefix(PlayerControl __instance, ref NetworkedPlayerInfo.PlayerOutfit __result)
    {
        __result = __instance.GetModInfo()?.Unbox().CurrentOutfit.Outfit.outfit!;
        return __result == null;
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.Initialize))]
class OverlayKillAnimationPatch
{
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] KillOverlayInitData initData)
    {
        if (__instance.killerParts)
        {
            NetworkedPlayerInfo.PlayerOutfit? currentOutfit = initData.killerOutfit;
            if (currentOutfit != null)
            {
                __instance.killerParts.SetBodyType(initData.killerBodyType);
                __instance.killerParts.UpdateFromPlayerOutfit(currentOutfit, PlayerMaterial.MaskType.None, false, false);
                __instance.killerParts.ToggleName(false);
                __instance.LoadKillerSkin(currentOutfit);
                __instance.LoadKillerPet(currentOutfit);
            }
        }
        if (__instance.victimParts)
        {
            NetworkedPlayerInfo.PlayerOutfit? defaultOutfit = initData.victimOutfit;
            if (defaultOutfit != null)
            {
                __instance.victimHat = defaultOutfit.HatId;
                __instance.victimParts.SetBodyType(PlayerBodyTypes.Normal);
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
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo.TaskInfo> tasks)
    {
        if (!__instance.AmOwner) return true;

        var info = __instance.GetModInfo();
        int num = tasks.Count;

        GameOperatorManager.Instance?.Update(); //イベントがあれば追加する(ゲーム開始の瞬間と被って、ホストでは通常のタイミングでの作用素の追加では間に合わない)
        var result = GameOperatorManager.Instance!.Run(new PlayerTasksTrySetLocalEvent(info, tasks.ToArray()));
        var tasksList = result.VanillaTasks.ToArray();
        
        if (num != tasksList.Length || result.ExtraQuota > 0) info?.Tasks.Unbox().ReplaceTasks(tasksList.Length, result.ExtraQuota);

        __instance.StartCoroutine(CoSetTasks().WrapToIl2Cpp());

        IEnumerator CoSetTasks()
        {
            while (!ShipStatus.Instance) yield return null;

            HudManager.Instance.TaskStuff.SetActive(true);

            for (int i = 0; i < __instance.myTasks.Count; i++) GameObject.Destroy(__instance.myTasks[i].gameObject);
            __instance.myTasks.Clear();

            __instance.Data.Role.SpawnTaskHeader(__instance);
            for (int i = 0; i < tasksList.Length; i++)
            {
                NetworkedPlayerInfo.TaskInfo taskInfo = tasksList[i];
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

        var unboxed = player.GetModInfo()!.Unbox();
        unboxed.IsDisconnected = true;
        unboxed.MyState = PlayerState.Disconnected;

        GameOperatorManager.Instance?.Run(new PlayerDisconnectEvent(unboxed), true);
        NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Disconnect, player.PlayerId, 0){ RelatedTag = EventDetail.Disconnect }) ;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter)]
class PlayerCanMovePatch
{
    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (__instance != PlayerControl.LocalPlayer) return;

        var modPlayer = __instance.GetModInfo();

        __result &= !TextField.AnyoneValid && HudManager.Instance.PlayerCam.Target == PlayerControl.LocalPlayer  && !ModSingleton<Marketplace>.Instance && !(modPlayer?.IsTeleporting ?? false);
    }
}

//指定位置への移動
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.WalkPlayerTo))]
class WalkPatch
{
    public static void Postfix(PlayerPhysics __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var info = __instance.myPlayer.GetModInfo()?.Unbox();
        if (info == null) return;

        var orig = __result;
        Vector4 temp = Vector2.zero;
        IEnumerator CoWalk()
        {
            while (true)
            {
                float speed = info.CalcSpeed(ref temp);
                if (speed < 0f) speed *= -1f;
                __instance.Speed = speed;

                if (!orig.MoveNext()) break;

                yield return null;
            }

            __instance.Speed = info.CalcSpeed(ref temp);
        }

        __result = CoWalk().WrapToIl2Cpp();
    }
}

//指定位置への移動
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.SetNormalizedVelocity))]
class VelocityPatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (__instance.AmOwner && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
        {
            var player = GamePlayer.LocalPlayer;
            if (player == null) return;

            var vec = __instance.body.velocity;
            var mat = GamePlayer.LocalPlayer!.Unbox().DirectionalPlayerSpeed;
            vec = new(vec.x * mat.x + vec.y * mat.y, vec.x * mat.z + vec.y * mat.w);

            __instance.body.velocity = vec;
        }
    }
}

//アップデートの影響でSetNormalizedVelocityがインライン化してしまったので、これの呼び出しにもパッチをあてる
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
class PlayerPhysicsFixedUpdatePatch
{
    public static bool Prefix(PlayerPhysics __instance)
    {
        NetworkedPlayerInfo data = __instance.myPlayer.Data;
        bool amDead = data != null && data.IsDead;
        __instance.HandleAnimation(amDead);
        if (__instance.AmOwner)
        {
            if (__instance.myPlayer.CanMove && GameData.Instance && DestroyableSingleton<HudManager>.InstanceExists && DestroyableSingleton<HudManager>.Instance.joystick != null)
            {
                __instance.SetNormalizedVelocity(DestroyableSingleton<HudManager>.Instance.joystick.DeltaL);
            }
            __instance.CheckCancelPetting();
        }

        return false;
    }
}

[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation), typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo))]
public static class KillOverlayPatch
{
    public static bool NextIsSelfKill = false;
    public static bool Prefix(KillOverlay __instance, NetworkedPlayerInfo killer, NetworkedPlayerInfo victim)
    {
        NextIsSelfKill = false;

        if (killer == null ||  killer.PlayerId == victim.PlayerId)
        {
            NextIsSelfKill = true;
            __instance.ShowKillAnimation(__instance.KillAnims[3], killer, victim);
            NextIsSelfKill = false;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation), typeof(OverlayKillAnimation), typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo))]
public static class KillOverlayBodyTypePatch
{
    public static bool NextIsSelfKill = false;
    public static bool Prefix(KillOverlay __instance, [HarmonyArgument(0)]OverlayKillAnimation killAnimation, [HarmonyArgument(1)] NetworkedPlayerInfo killer, [HarmonyArgument(2)] NetworkedPlayerInfo victim)
    {
        var info = new KillOverlayInitData(killer, victim);
        info.killerBodyType = Helpers.GetPlayer(killer.PlayerId)?.MyPhysics.bodyType ?? PlayerBodyTypes.Normal;
        __instance.ShowKillAnimation(killAnimation, info);
        return false;
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.Initialize))]
public static class OverlayKillAnimationInitializePatch
{

    public static void Postfix(OverlayKillAnimation __instance)
    {
        if (KillOverlayPatch.NextIsSelfKill)
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
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] NetworkedPlayerInfo.PlayerOutfit victimOutfit)
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
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] NetworkedPlayerInfo.PlayerOutfit killerOutfit)
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
    private static void SetGoalPos(PlayerControl target, Vector2 pos)
    {
        try
        {
            target.GetModInfo()!.Unbox().GoalPos = pos;
        }
        catch
        {
            Debug.Log($"Skipped presetting goal position on use MovingPlatform. (for {target.name})");
        }
    }
    public static bool Prefix(MovingPlatformBehaviour __instance, ref Il2CppSystem.Collections.IEnumerator __result, [HarmonyArgument(0)]PlayerControl target)
    {
        __result = CoUsePlatform().WrapToIl2Cpp();
        return false;
        System.Collections.IEnumerator CoUsePlatform()
        {
            float platformTime = Time.time;
            float totalTime = Time.time;
            __instance.Target = target;
            target.MyPhysics.ResetMoveState(true);
            if (target.AmOwner) PlayerControl.HideCursorTemporarily();
            target.moveable = false;
            target.NetTransform.SetPaused(true);
            target.NetTransform.ClearPositionQueues();
            target.SetKinematic(true);
            target.inMovingPlat = true;
            target.ForceKillTimerContinue = true;
            Vector3 vector = __instance.IsLeft ? __instance.LeftUsePosition : __instance.RightUsePosition;
            Vector3 vector2 = (!__instance.IsLeft) ? __instance.LeftUsePosition : __instance.RightUsePosition;
            Vector3 sourcePos = __instance.IsLeft ? __instance.LeftPosition : __instance.RightPosition;
            Vector3 targetPos = (!__instance.IsLeft) ? __instance.LeftPosition : __instance.RightPosition;
            Vector3 worldUseSourcePos = __instance.transform.parent.TransformPoint(vector);
            Vector3 worldUseTargetPos = __instance.transform.parent.TransformPoint(vector2);
            Vector3 worldSourcePos = __instance.transform.parent.TransformPoint(sourcePos);
            Vector3 worldTargetPos = __instance.transform.parent.TransformPoint(targetPos);

            SetGoalPos(target, worldUseSourcePos);

            yield return target.MyPhysics.WalkPlayerTo(worldUseSourcePos, 0.01f, 1f, false);
            yield return target.MyPhysics.WalkPlayerTo(worldSourcePos, 0.01f, 1f, false);

            SetGoalPos(target, worldUseTargetPos);

            yield return Effects.Wait(0.1f);
            worldSourcePos -= (Vector3)target.Collider.offset;
            worldTargetPos -= (Vector3)target.Collider.offset;
            if (Constants.ShouldPlaySfx()) SoundManager.Instance.PlayDynamicSound("PlatformMoving", __instance.MovingSound, true, (GetDynamicsFunction)__instance.SoundDynamics, SoundManager.Instance.SfxChannel);

            __instance.IsLeft = !__instance.IsLeft;
            yield return Effects.All(
            (Il2CppSystem.Collections.IEnumerator[])[
            Effects.Slide2D(__instance.transform, sourcePos, targetPos, target.MyPhysics.Speed),
            Effects.Slide2DWorld(target.transform, worldSourcePos, worldTargetPos, target.MyPhysics.Speed)
            ]);
            if (Constants.ShouldPlaySfx()) SoundManager.Instance.StopNamedSound("PlatformMoving");
            if (target == null)
            {
                __instance.ResetPlatform();
                yield break;
            }

            yield return target.MyPhysics.WalkPlayerTo(worldUseTargetPos, 0.01f, 1f, false);
            target.SetPetPosition(target.transform.position);
            target.inMovingPlat = false;
            target.moveable = true;
            target.ForceKillTimerContinue = false;
            target.NetTransform.SetPaused(false);
            target.SetKinematic(false);
            target.NetTransform.Halt();

            target.GetModInfo()?.Unbox().ResetDeadBodyGoalPos();

            yield return Effects.Wait(0.1f);
            __instance.Target = null;
            yield break;
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
            __instance.myPlayer.GetModInfo()!.Unbox().GoalPos = pos;
        }
        catch
        {
            Debug.Log($"Skipped presetting goal position on climbing Ladder. (for {__instance.name})");
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoClimbLadder))]
public static class CoUseLadderPatch
{
    public static void Postfix(PlayerPhysics __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = Effects.Sequence(__result, ManagedEffects.Action(() => {
            __instance.myPlayer.GetModInfo()?.Unbox().ResetDeadBodyGoalPos();
        }).WrapToIl2Cpp());
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Deserialize))]
internal class RPCSealingPatch
{
    static private bool lastIsDead = false;
    static void Prefix(NetworkedPlayerInfo __instance, [HarmonyArgument(1)] ref bool initialState)
    {
        lastIsDead = __instance.IsDead;
        initialState = true;
    }

    static void Postfix(NetworkedPlayerInfo __instance)
    {
        __instance.IsDead = lastIsDead || __instance.Disconnected;
    }
}

[NebulaRPCHolder]
[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
class CustomNetworkTransformPatch
{
    
    public static void Prefix(CustomNetworkTransform __instance)
    {
        if (__instance.isPaused && __instance.incomingPosQueue.Count > 0) __instance.incomingPosQueue.Clear();

        //rubberbandModifierをいじる関数にフックできないのでここで変更(バニラの変更を考慮して大きめな値にしておく)
        if (GeneralConfigurations.LowLatencyPlayerSyncOption && (AmongUsUtil.IsCustomServer() || AmongUsUtil.IsLocalServer()))
        {
            float num = __instance.incomingPosQueue.Count switch
            {
                < 2 => 1.3f, //0.5にしようとしてくるやつを抑制するので大きめに
                3 => 1.8f, //0.5にしようとしてくるやつを抑制するので大きめに
                4 or 5 => 2.5f, //0.5にしようとしてくるやつを抑制するので大きめに
                _ => 1.8f
            };
            __instance.rubberbandModifier = Mathf.Lerp(__instance.rubberbandModifier, num, Time.fixedDeltaTime * 3f);
        }
    }
    
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.PlayStepSound))]
public static class PlayerFootstepPatch
{
    static bool Prefix(PlayerControl __instance)
    {
        if (!Constants.ShouldPlaySfx()) return false;
        
        if (LobbyBehaviour.Instance)
        {
            if (__instance.AmOwner)
            {
                for (int i = 0; i < LobbyBehaviour.Instance.AllRooms.Length; i++)
                {
                    SoundGroup soundGroup = LobbyBehaviour.Instance.AllRooms[i].MakeFootstep(__instance);
                    if (soundGroup)
                    {
                        AudioClip clip = soundGroup.Random();
                        __instance.FootSteps.clip = clip;
                        __instance.FootSteps.Play();
                        break;
                    }
                }
            }
            return false;
        }
        if (!ShipStatus.Instance) return false;

        bool canHear = __instance.AmOwner;
        if (!canHear && GeneralConfigurations.CanHearOthersFootstepOption)
        {
            var modPlayer = __instance.GetModInfo();
            var modInfo = modPlayer?.Unbox();
            canHear |= (modInfo?.VisibilityLevel ?? 2) <= 1 && !(modPlayer?.IsDived ?? false);
            
        }
        if (canHear)
        {
            for (int j = 0; j < ShipStatus.Instance.AllStepWatchers.Length; j++)
            {
                SoundGroup soundGroup2 = ShipStatus.Instance.AllStepWatchers[j].MakeFootstep(__instance);
                if (soundGroup2)
                {
                    AudioClip clip2 = soundGroup2.Random();
                    __instance.FootSteps.clip = clip2;
                    __instance.FootSteps.Play();
                    return false;
                }
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(SkeldShipRoom), nameof(SkeldShipRoom.MakeFootstep))]
public static class SkeldFootstepPatch
{
    static bool Prefix(SkeldShipRoom __instance, ref SoundGroup __result, [HarmonyArgument(0)]PlayerControl player)
    {
        if (player.AmOwner) return true;

        if (__instance.roomArea.OverlapPoint(player.GetTruePosition())) __result = __instance.FootStepSounds;
        
        return false;
    }
}
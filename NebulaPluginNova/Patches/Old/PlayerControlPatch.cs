using AmongUs.Data.Player;
using AmongUs.GameOptions;
using Hazel;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using Nebula.Online;
using Nebula.Roles;
using PowerTools;
using Sentry;
using UnityEngine;
using Virial;
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
        //フックショットや斧用の壁と当たらないようにする
        if (__instance.rigidbody2D.AsBoolFast(out var rigidBody)) rigidBody.excludeLayers |= 1 << LayerExpansion.GetHookshotWallLayer();

        __result = Effects.Sequence(
            __result,
            Effects.Action((Il2CppSystem.Action)(()=>
            {
                __instance.SetColor(__instance.PlayerId);
                if (AmongUsLLImpl.LocalPlayer) DynamicPalette.RpcShareMyColor();
                if (__instance.AmOwner)
                {
                    __instance.lightSource.lightChild.layer = LayerExpansion.GetVanillaShadowLightLayer();
                }

                __instance.cosmetics.nameText.UseRoleIcon();
            }))
            );

        //人数が多いと近くのコンソールも追跡できなくなるので、上限を緩和
        __instance.hitBuffer = new Collider2D[120];
    }
    
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


internal static class HighlightManager
{
    static private List<(Renderer, Material)> lastHighlightRenderers = [];
    static public void AddHighlightedRenderer(Renderer renderer) => lastHighlightRenderers.Add((renderer, renderer.material));
    static public IEnumerable<(Renderer renderer, Material material)> HighlightedRenderers => lastHighlightRenderers;
    static public void Reset() => lastHighlightRenderers.Clear();
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class PlayerUpdatePatch
{
    /*
    static IEnumerable<SpriteRenderer> AllHighlightable()
    {
        foreach (var p in NebulaGameManager.Instance?.AllPlayerlike ?? []) if(p.IsActive) yield return p.VanillaCosmetics.currentBodySprite.BodySprite;
        foreach (var d in Helpers.AllDeadBodies()) if(d) foreach (var r in d.bodyRenderers) if(r) yield return r;
        if (ShipStatus.Instance)
        {
            foreach (var v in ShipStatus.Instance.AllVents.GetFastEnumerator()) yield return v.myRend;
            foreach (var c in ShipStatus.Instance.AllConsoles.GetFastEnumerator()) if(c.Image) yield return c.Image;
        }
        foreach (var cc in ModSingleton<CustomConsoleManager>.Instance.AllCustomConsoles) yield return cc.Renderer;
    }
    */

    static private float lastKillTimer = 0f;
    static internal void EditLocalKillTimer(float timer)
    {
        lastKillTimer = timer;
    }

    static void Prefix(PlayerControl __instance)
    {
        lastKillTimer = __instance.killTimer;

        if (__instance.AmOwner)
        {
            foreach(var r in HighlightManager.HighlightedRenderers) if(r.renderer.AsBoolFast()) r.material.SetFloat("_Outline", 0f);
            HighlightManager.Reset();
        }
    }

    
    static void Postfix(PlayerControl __instance)
    {
        if (NebulaGameManager.Instance == null) return;

        var deltaTime = Time.fixedDeltaTime;

        if (__instance.AmOwner) NebulaGameManager.Instance.OnFixedAlwaysUpdate(deltaTime);

        if (NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted)
        {
            //ロビーでの名前

            bool showVanillaColor = ClientOption.GetValue(ClientOption.ClientOptionType.ShowVanillaColor) == 1;
            try
            {
                var nameText = __instance.cosmetics.nameText;
                var text = nameText.text;
                if (showVanillaColor) text = __instance.Data.PlayerName + " ■".Color(DynamicPalette.VanillaColorsPalette[__instance.PlayerId]);
                else text = __instance.Data.PlayerName;

                //オンライン認証
                if (NoSAuth.IsAuthRequired)
                {
                    var auth = NoSAuth.Get(__instance.Data.ClientId);
                    switch (auth.Status)
                    {
                        case AuthStatus.Unknown:
                        case AuthStatus.Unregistered:
                        case AuthStatus.Unavailable:
                        case AuthStatus.NoResponse:
                            text = text.Color(Color.gray);
                            break;
                        case AuthStatus.Verified:
                            if (auth.Uid == null) break;
                            var data = PlayerNameHistory.Observe(auth, __instance.Data);
                            if (data.HasValue)
                            {
                                if (data.Value.IsFriend) text += TextIcon.Heart.GetTextIconTag();
                                if (data.Value.IsBlocked) text += TextIcon.Caution.GetTextIconTag();
                                if (data.Value.Experience <= 1) text += TextIcon.Leaf.GetTextIconTag();
                                if (data.Value.UsedByOthers) text += TextIcon.Info.GetTextIconTag();
                            }
                            break;
                    }
                }

                nameText.text = text;
                nameText.transform.parent.gameObject.SetActive(!ModSingleton<ShowUp>.Instance.AnyoneShowedUp);
            }
            catch { }

            return;
        }

        var modPlayer = NebulaGameManager.Instance.GetPlayer(__instance.PlayerId);
        modPlayer?.Unbox().Update(deltaTime);

        if (modPlayer?.AmOwner ?? false)
        {
            NebulaGameManager.Instance.OnFixedUpdate(deltaTime);

            //ペット・レポートボタンの使用可否
            if (AmongUsLLImpl.TryGetHudManager(out var hudManager) && modPlayer.IsDived)
            {
                hudManager.ReportButton.SetDisabled();
                hudManager.PetButton.SetDisabled();
            }
            else if (!NebulaGameManager.Instance.LocalStatus.CanReport)
            {
                hudManager.ReportButton.SetDisabled();
            }

            if (__instance.inVent && Vent.currentVent.AsBoolFast(out var currentVent))
            {
                VVector2 vector = currentVent.ModGameObject(false).Position;
                vector -= (VVector2)__instance.Collider.offset;

                if (__instance.MyPhysics.body.ModGameObject(false).Position.Distance(vector) > 0.001f) __instance.NetTransform.RpcSnapTo(vector);
            }

            //キルボタンのクールダウン進行
            {
                var data = __instance.Data;
                bool flag = data.Role.CanUseKillButton && !data.IsDead;
                if ((__instance.IsKillTimerEnabled || __instance.ForceKillTimerContinue) && flag)
                {
                    float coeff = GamePlayer.LocalPlayer?.Unbox().CalcAttributeVal(PlayerAttributes.CooldownSpeed, true) ?? 1f;
                    deltaTime *= coeff;
                    __instance.SetKillTimer(lastKillTimer - deltaTime);
                }
            }
        }

        var transform = modPlayer.VanillaCosmetics.ModGameObject(false);
        if (transform.LocalScale.z < 100f)
        {
            var scale = transform.LocalScale;
            scale.z = 100f;
            transform.LocalScale = scale;
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

        NebulaGameManager.Instance.GetPlayer(__instance.PlayerId)?.Unbox().UpdateVisibility(null, false);
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
        if (__instance.AmOwner && (NebulaGameManager.Instance?.GameState ?? 0) < NebulaGameStates.WaitGameResult)
        {
            GameOperatorManager.Instance?.Run(new PlayerTaskRemoveLocalEvent(GamePlayer.LocalPlayer, task));
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

//会議の始まりは MeetingHudExtension.ModStartMeeting で記録する。
//PlayerControl.StartMeeting は ReportDeadBody のパッチが return false で迂回していて呼ばれない。

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CurrentOutfit), MethodType.Getter)]
class CurrentOutfitPatch
{
    public static bool Prefix(PlayerControl __instance, ref NetworkedPlayerInfo.PlayerOutfit __result)
    {
        __result = __instance.GetModInfo()?.CurrentOutfit.outfit!;
        return __result == null;
    }
}

[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.Initialize))]
class OverlayKillAnimationPatch
{
    public static bool Prefix(OverlayKillAnimation __instance, [HarmonyArgument(0)] KillOverlayInitData initData)
    {
        if (__instance.killerParts.AsBoolFast(out var killerParts))
        {
            NetworkedPlayerInfo.PlayerOutfit? currentOutfit = initData.killerOutfit;
            if (currentOutfit != null)
            {
                killerParts.SetBodyType(initData.killerBodyType);
                killerParts.UpdateFromPlayerOutfit(currentOutfit, PlayerMaterial.MaskType.None, false, false);
                killerParts.ToggleName(false);
                __instance.LoadKillerSkin(currentOutfit);
                __instance.LoadKillerPet(currentOutfit);
            }
        }
        if (__instance.victimParts.AsBoolFast(out var victimParts))
        {
            NetworkedPlayerInfo.PlayerOutfit? defaultOutfit = initData.victimOutfit;
            if (defaultOutfit != null)
            {
                __instance.victimHat = defaultOutfit.HatId;
                victimParts.SetBodyType(PlayerBodyTypes.Normal);
                victimParts.UpdateFromPlayerOutfit(defaultOutfit, PlayerMaterial.MaskType.None, false, false);
                victimParts.ToggleName(false);
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
            while (!AmongUsLLImpl.ShipStatusInstance.AsBoolFast()) yield return null;

            HudManager.Instance.TaskStuff.SetActive(true);

            for (int i = 0; i < __instance.myTasks.Count; i++) GameObject.Destroy(__instance.myTasks[i].gameObject);
            __instance.myTasks.Clear();

            __instance.Data.Role.SpawnTaskHeader(__instance);
            for (int i = 0; i < tasksList.Length; i++)
            {
                NetworkedPlayerInfo.TaskInfo taskInfo = tasksList[i];
                NormalPlayerTask normalPlayerTask = GameObject.Instantiate<NormalPlayerTask>(AmongUsLLImpl.ShipStatusInstance.GetTaskById(taskInfo.TypeId), __instance.transform);
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
    //特別に移動を許可するプレイヤーカメラ
    private static int? canMoveCameraId = null;
    public static void SetMovableCamera(MonoBehaviour behaviour)
    {
        canMoveCameraId = behaviour.GetInstanceIdFast();
    }
    private static bool CameraAllowMoving()
    {
        var currentTarget = AmongUsLLImpl.HudManagerInstance.PlayerCam.Target;
        if (currentTarget.EqualsFast(AmongUsLLImpl.LocalPlayer)) return true;
        if (currentTarget && currentTarget.GetInstanceIdFast() == canMoveCameraId) return true;
        return false;
    }

    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (__instance != AmongUsLLImpl.LocalPlayer) return;

        var modPlayer = __instance.GetModInfo();

        __result &= !TextField.AnyoneValid && CameraAllowMoving() && !ModSingleton<Marketplace>.Instance.AsBoolFast() && !(modPlayer?.IsTeleporting ?? false);
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

        VVector4 temp = new(0f, 0f, 0f, 0f);
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
        PlayerControl control = __instance.myPlayer;
        NetworkedPlayerInfo data = control.Data;
        bool amDead = data.AsBoolFast() && data.IsDead;
        __instance.HandleAnimation(amDead);
        if (__instance.AmOwner)
        {
            if (control.CanMove && GameData.Instance.AsBoolFast() && DestroyableSingleton<HudManager>.InstanceExists && DestroyableSingleton<HudManager>.Instance.joystick != null)
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
            //GetChild(2)とGetChild(3)はペットがいる場合。自殺の場合ペットの有無はキラーと被害者で同じなのでこの決め打ちでよい。

            var transform = __instance.transform;
            int childCount = transform.childCount;
            bool hasPet = childCount >= 3;

            transform.GetChild(0).gameObject.SetActive(false);
            if(hasPet) transform.GetChild(2).gameObject.SetActive(false);

            float x = 0.6f;
            var victim = transform.GetChild(1);
            victim.localPosition = new(x, 0f, 0f);
            Transform? pet = null;
            if (hasPet)
            {
                pet = transform.GetChild(3);
                pet.localPosition = new(x, -0.37f, 0f);
            }

            IEnumerator CoMoveLeft()
            {
                yield return new WaitForSeconds(0.44f);
                while(x > -1.8f)
                {
                    if (!__instance) yield break;
                    x -= (x - (-1.8f)) * Time.deltaTime * 5.2f;
                    victim!.localPosition = new(x, 0f, 0f);
                    if(hasPet) pet!.localPosition = new(x, -0.37f, 0f);
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
        SkinViewData skin = AmongUsLLImpl.ShipStatusInstance.CosmeticsCache.GetSkin(victimOutfit.SkinId);
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
        SkinViewData skin = AmongUsLLImpl.ShipStatusInstance.CosmeticsCache.GetSkin(killerOutfit.SkinId);
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
            bool isLeft = __instance.IsLeft;
            var parent = __instance.transform.parent;
            Vector3 vector = isLeft ? __instance.LeftUsePosition : __instance.RightUsePosition;
            Vector3 vector2 = (!isLeft) ? __instance.LeftUsePosition : __instance.RightUsePosition;
            Vector3 sourcePos = isLeft ? __instance.LeftPosition : __instance.RightPosition;
            Vector3 targetPos = (!isLeft) ? __instance.LeftPosition : __instance.RightPosition;
            Vector3 worldUseSourcePos = parent.TransformPointFast(vector);
            Vector3 worldUseTargetPos = parent.TransformPointFast(vector2);
            Vector3 worldSourcePos = parent.TransformPointFast(sourcePos);
            Vector3 worldTargetPos = parent.TransformPointFast(targetPos);

            var modPlayer = target.GetModInfo();

            modPlayer?.DeathPosition = new(worldUseSourcePos, worldUseSourcePos);
            
            yield return target.MyPhysics.WalkPlayerTo(worldUseSourcePos, 0.01f, 1f, false);
            yield return target.MyPhysics.WalkPlayerTo(worldSourcePos, 0.01f, 1f, false);

            yield return Effects.Wait(0.1f);
            worldSourcePos -= (Vector3)target.Collider.offset;
            worldTargetPos -= (Vector3)target.Collider.offset;
            if (Constants.ShouldPlaySfx()) AmongUsLLImpl.SoundManagerInstance.PlayDynamicSound("PlatformMoving", __instance.MovingSound, true, (GetDynamicsFunction)__instance.SoundDynamics, AmongUsLLImpl.SoundManagerInstance.SfxChannel);

            modPlayer?.DeathPosition = new(worldUseSourcePos, worldUseTargetPos);

            __instance.IsLeft = !__instance.IsLeft;
            yield return Effects.All(
            (Il2CppSystem.Collections.IEnumerator[])[
            Effects.Slide2D(__instance.transform, sourcePos, targetPos, /*target.MyPhysics.Speed*/ 2.5f),
            Effects.Slide2DWorld(target.transform, worldSourcePos, worldTargetPos, /*target.MyPhysics.Speed*/ 2.5f)
            ]);
            if (Constants.ShouldPlaySfx()) AmongUsLLImpl.SoundManagerInstance.StopNamedSound("PlatformMoving");
            if (target == null)
            {
                __instance.ResetPlatform();
                yield break;
            }

            if(modPlayer != null) GameOperatorManager.Instance?.Run<PlayerUseMovingPlatformEvent>(new(modPlayer, sourcePos, targetPos));


            yield return target.MyPhysics.WalkPlayerTo(worldUseTargetPos, 0.01f, 1f, false);
            target.SetPetPosition(target.transform.GetPositionFast());
            target.inMovingPlat = false;
            target.moveable = true;
            target.ForceKillTimerContinue = false;
            target.NetTransform.SetPaused(false);
            target.SetKinematic(false);
            target.NetTransform.Halt();

            modPlayer?.DeathPosition = null;

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
            Vector2 GetPosition(Ladder ladder) => ladder.IsTop ? (Vector2)ladder.transform.GetPositionFast() + new Vector2(0f, 1.2f) : ladder.transform.GetPositionFast();
            
            __instance.myPlayer.GetModInfo()!.DeathPosition = new(GetPosition(source), GetPosition(source.Destination));
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
            __instance.myPlayer.GetModInfo()?.DeathPosition = null;
        }).WrapToIl2Cpp());
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Deserialize))]
internal class NetworkedPlayerInfoPatch
{
    static bool Prefix(NetworkedPlayerInfo __instance, [HarmonyArgument(0)] Hazel.MessageReader reader, [HarmonyArgument(1)] bool initialState)
    {
        Virial.Utilities.MessageReader virialReader;
        
        virialReader = Virial.Utilities.MessageReader.Get(reader);

        __instance.PlayerId = virialReader.ReadByte();
        __instance.ClientId = virialReader.ReadPackedInt32();
        byte b = virialReader.ReadByte();
        __instance.Outfits.Clear();

        virialReader.End();

        for (int i = 0; i < (int)b; i++)
        {
            PlayerOutfitType playerOutfitType = (PlayerOutfitType)reader.ReadByte();
            NetworkedPlayerInfo.PlayerOutfit playerOutfit = new NetworkedPlayerInfo.PlayerOutfit();
            playerOutfit.Deserialize(reader);
            __instance.Outfits[playerOutfitType] = playerOutfit;
        }

        virialReader = Virial.Utilities.MessageReader.Get(reader);

        __instance.PlayerLevel = virialReader.ReadPackedUInt32();
        byte b2 = virialReader.ReadByte();
        __instance.Disconnected = (b2 & 1) > 0;
        //__instance.IsDead = (b2 & 4) > 0;
        __instance.RoleType = (RoleTypes)virialReader.ReadUInt16();

        if (virialReader.ReadBoolean())
        {
            __instance.RoleWhenAlive = new((RoleTypes)virialReader.ReadUInt16());
        }
        byte b3 = virialReader.ReadByte();
        __instance.Tasks.Clear();

        virialReader.End();

        for (int j = 0; j < (int)b3; j++)
        {
            NetworkedPlayerInfo.TaskInfo taskInfo = new NetworkedPlayerInfo.TaskInfo();
            taskInfo.Deserialize(reader);
            __instance.Tasks.Add(taskInfo);
        }

        __instance.FriendCode = reader.ReadString();
        __instance.Puid = reader.ReadString();
        if (initialState && GameData.Instance.GetPlayerById(__instance.PlayerId) == null && !GameData.Instance.IsProcessingInfo(__instance))
        {
            GameData.Instance.AddPlayerInfo(__instance);
        }
        if (!initialState && __instance.Object != null)
        {
            __instance.Object.MyPhysics.ResetAnimState();
        }
        //GameData.Instance.RecomputeTaskCounts();

        return false;
    }
}


[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.OnEnable))]
public class SyncTransformPatch
{
    public static void Postfix(CustomNetworkTransform __instance)
    {
        __instance.sendQueue.Clear();
        __instance.incomingPosQueue.Clear();
        var pos = __instance.ModGameObject(false).Position.AsVector2().ToUnityVector();
        __instance.lastPosition = pos;
        __instance.lastPosSent = pos;
    }
}


[NebulaRPCHolder]
[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
class CustomNetworkTransformPatch
{
    
    public static void Prefix(CustomNetworkTransform __instance)
    {
        var amOwner = __instance.AmOwner;
        if (__instance.isPaused && amOwner && __instance.sendQueue.Count > 0) __instance.sendQueue.Clear();
        if(amOwner && __instance.sendQueue.Count == 0) __instance.lastPosSent = __instance.ModGameObject(false).Position.AsVector2();

        if (__instance.isPaused && __instance.incomingPosQueue.Count > 0) __instance.incomingPosQueue.Clear();
        if (__instance.incomingPosQueue.Count == 0) __instance.lastPosition = __instance.ModGameObject(false).Position.AsVector2();

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
            __instance.rubberbandModifier = Mathn.Lerp(__instance.rubberbandModifier, num, FastMethods.GetFixedDeltaTimeFast() * 3f);
        }
    }
    
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.PlayStepSound))]
public static class PlayerFootstepPatch
{
    static bool Prefix(PlayerControl __instance)
    {
        if (!Constants.ShouldPlaySfx()) return false;

        var lobby = AmongUsLLImpl.LobbyInstance;
        if (lobby.AsBoolFast())
        {
            if (__instance.AmOwner)
            {
                for (int i = 0; i < lobby.AllRooms.Length; i++)
                {
                    SoundGroup soundGroup = lobby.AllRooms[i].MakeFootstep(__instance);
                    if (soundGroup.AsBoolFast())
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

        var ship = AmongUsLLImpl.ShipStatusInstance;

        if (!ship.AsBoolFast()) return false;

        bool canHear = __instance.AmOwner;
        if (!canHear && GeneralConfigurations.CanHearOthersFootstepOption)
        {
            var modPlayer = __instance.GetModInfo();
            canHear |= !(modPlayer?.IsInvisible ?? true) && !(modPlayer?.IsDived ?? false);
            
        }

        if (canHear)
        {
            if (GameOperatorManager.Instance?.Run(new PlayerCheckPlayFootSoundEvent(__instance.GetModInfo()!)).PlayFootSound ?? true)
            {
                var watchers = ship.AllStepWatchers;
                var watchersLength = watchers.Length;

                for (int j = 0; j < watchersLength; j++)
                {
                    SoundGroup soundGroup2 = watchers[j].MakeFootstep(__instance);
                    if (soundGroup2)
                    {
                        AudioClip clip2 = soundGroup2.Random();
                        __instance.FootSteps.clip = clip2;
                        __instance.FootSteps.Play();
                        return false;
                    }
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

[HarmonyPatch(typeof(PetBehaviour), nameof(PetBehaviour.Start))]
public class PetBehaviourStartPatch
{
    public static void Postfix(PetBehaviour __instance)
    {
        var parent = __instance.transform.parent;
        if (parent.AsBoolFast() && parent.gameObject.layer == LayerExpansion.GetUILayer())
        {
            __instance.gameObject.layer = LayerExpansion.GetUILayer();
            __instance.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetUILayer());
        }
        else
        {
            __instance.gameObject.layer = LayerExpansion.GetPlayersLayer();
            __instance.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetPlayersLayer());
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.LateUpdate))]
public static class PlayerPhysicsPatch
{
    static bool Prefix(PlayerPhysics __instance)
    {
        if (AmongUsClient.Instance.GameState >= InnerNet.InnerNetClient.GameStates.Started)
        {
            var position = __instance.ModGameObject(false).Position;
            var ev = GameOperatorManager.Instance?.Run(new PlayerFixZPositionEvent(__instance.myPlayer.GetModInfo()!, position.y));
            if (ev != null)
            {
                __instance.transform.SetWorldZ(ev.CalcZ);
            }
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
class ResetMoveStatePatch
{
    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        if (callId == 19)
        {
            //EnterVent, はしご移動によるNetTransformの更新停止を切る。
            __instance.myPlayer.ChangeMoveMode(true);
            if (__instance.Animations.IsPlayingAnyLadderAnimation()) __instance.Animations.PlayIdleAnimation();
        }
    }
}

[HarmonyPatch(typeof(ViperDeadBody), nameof(ViperDeadBody.FixedUpdate))]
public class BlockViperDeadBodyUpdatePatch
{
    public static bool Prefix(ViperDeadBody __instance) => false;
}

[HarmonyPatch(typeof(DeadBody), nameof(DeadBody.OnClick))]
public class DissolvedDeadBodyClickPatch
{
    readonly public static byte DissolvedDeadBodyMask = 0x80;
    public static bool Prefix(DeadBody __instance)
    {
        if((__instance.ParentId & DissolvedDeadBodyMask) != 0)
        {
            if (__instance.Reported) return false;
            if (!(NebulaGameManager.Instance?.LocalStatus.CanReport ?? false)) return false;

            var localPlayer = AmongUsLLImpl.LocalPlayer;
            Vector2 truePosition = localPlayer.GetTruePosition();
            Vector2 truePosition2 = __instance.TruePosition;
            if (Vector2.Distance(truePosition2, truePosition) <= localPlayer.MaxReportDistance && localPlayer.CanMove && !PhysicsHelpers.AnythingBetween(truePosition, truePosition2, Constants.ShipAndObjectsMask, false))
            {
                __instance.Reported = true;
                MeetingHudExtension.ModCmdReportDeadBody(GamePlayer.LocalPlayer, GamePlayer.GetPlayer((byte)(__instance.ParentId & ~DissolvedDeadBodyMask)), MeetingHudExtension.ReportType.ReportDissolvedBody, true, false);
            }
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerNameColor), nameof(PlayerNameColor.Get), [typeof(NetworkedPlayerInfo)])]
public class PlayerNameColorChatPatch
{
    public static bool Prefix(ref UnityEngine.Color __result, [HarmonyArgument(0)] NetworkedPlayerInfo pc)
    {
        var player = GamePlayer.GetPlayer(pc.PlayerId);
        if (player != null)
        {
            var ev = GameOperatorManager.Instance?.Run(new PlayerDecorateNameEvent(player, "", NebulaGameManager.Instance?.CanSeeAllInfo ?? false));
            __result = ev.Color?.ToUnityColor() ?? Color.white;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.GetTruePosition))]
public class PlayerGetTruePositionPatch
{
    public static bool Prefix(PlayerControl __instance, ref Vector2 __result)
    {
        var transform = __instance.ModGameObject();
        __result = transform.Position.AsVector2() + (VVector2)__instance.Collider.offset * transform.LocalScale.x;
        return false;
    }
}
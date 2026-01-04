using AmongUs.Data;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using Virial.DI;
using Virial.Events.Player;
using static Nebula.Modules.HelpScreen;

namespace Nebula.Patches;


[HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
public static class HudManagerStartPatch
{
    static void Prefix(HudManager __instance)
    {
        NebulaGameManager.Instance?.Abandon();
    }
    static void Postfix(HudManager __instance)
    {
        DIManager.Instance.Instantiate<Virial.Game.Game>();
        
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Light(Dummy)", AmongUsUtil.GetShadowCollab().ShadowCamera.transform, new Vector3(0, 0, -4f), LayerExpansion.GetDrawShadowsLayer());
        renderer.sprite = VanillaAsset.FullScreenSprite;
        renderer.material.shader = NebulaAsset.StoreBackShader;
        renderer.color = Color.clear;
        
        __instance.TaskPanel.transform.localPosition = new(0, 0, 0);

        IEnumerator CoUpdatePos(NotificationPopper popper)
        {
            void UpdateZ(float z)
            {
                var edge = popper.aspectPosition.DistanceFromEdge;
                edge.z = z;
                popper.aspectPosition.DistanceFromEdge = edge;
            }
            while (true)
            {
                if (LobbyBehaviour.Instance) UpdateZ(-34f);
                if (ShipStatus.Instance) UpdateZ(-800f);
                yield return null;
            }
        }
        __instance.Notifier.StartCoroutine(CoUpdatePos(__instance.Notifier).WrapToIl2Cpp());
    }
}

#if ANDROID
[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetTouchType))]
public static class SetJoyStickSizePatch
{
    public static void Postfix(HudManager __instance, [HarmonyArgument(0)] ControlTypes type)
    {
        if(type == ControlTypes.VirtualJoystick)
        {
            MonoBehaviour monoBehaviour2 = GameObject.Instantiate<MonoBehaviour>(__instance.RightVJoystick);
            if (monoBehaviour2 != null)
            {
                monoBehaviour2.transform.SetParent(__instance.transform, false);
                __instance.joystickR = monoBehaviour2.GetComponent<VirtualJoystick>();
                __instance.joystickR.ToggleVisuals(LobbyBehaviour.Instance == null);
            }

            var leftAspectPos = __instance.joystick.CastFast<VirtualJoystick>().gameObject.GetComponent<AspectPosition>();
            leftAspectPos.DistanceFromEdge = new(0.7f, 0.64f, -10f);
            leftAspectPos.transform.localScale = new(0.85f, 0.85f, 1f);
            leftAspectPos.AdjustPosition();

            var rightAspectPos = __instance.joystickR.gameObject.GetComponent<AspectPosition>();
            rightAspectPos.Alignment = AspectPosition.EdgeAlignments.RightBottom;
            rightAspectPos.DistanceFromEdge = new(0.7f, 0.64f, -10f);
            rightAspectPos.transform.localScale = new(0.85f, 0.85f, 1f);
            rightAspectPos.AdjustPosition();
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.AdjustLighting))]
public static class SetFlashlightInputMethodPatch
{
    public static bool Prefix() => false;
}
#endif


[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HudManagerUpdatePatch
{
    static void Postfix(HudManager __instance)
    {
        __instance.UpdateHudContent();
        NebulaGameManager.Instance?.OnUpdate();
        NebulaGameManager.Instance?.AllPlayerInfo.Do(p => p.Unbox().HudUpdate());

        if (!TextField.AnyoneValid &&  NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Help).KeyDownForAction && !IntroCutscene.Instance && !Minigame.Instance && !ExileController.Instance)
        {
            HelpScreen.TryOpenHelpScreen(HelpTab.MyInfo);
        }
    }
}


[HarmonyPatch(typeof(HudManager), nameof(HudManager.CoShowIntro))]
public static class HudManagerCoStartGamePatch
{
    static bool Prefix(HudManager __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        IEnumerator GetEnumerator(){
            //UIを閉じる
            NebulaManager.Instance.CloseAllUI();

            while (!ShipStatus.Instance)
            {
                yield return null;
            }
            __instance.IsIntroDisplayed = true;
            DestroyableSingleton<HudManager>.Instance.FullScreen.transform.localPosition = new Vector3(0f, 0f, -250f);

            //スタンプの位置を変更
            StampHelpers.SetStampShowerToUnderHud(() => !GameManager.Instance.GameHasStarted);

            yield return DestroyableSingleton<HudManager>.Instance.ShowEmblem(true);
            IntroCutscene introCutscene = GameObject.Instantiate<IntroCutscene>(__instance.IntroPrefab, __instance.transform);
            yield return introCutscene.CoBegin();

            yield return ModPreSpawnInPatch.ModPreSpawnIn(__instance.transform, GameStatistics.EventVariation.GameStart, EventDetail.GameStart);

            float killCooldown = GeneralConfigurations.UseShortenCooldownAtGameStartOption ? ModAbilityButtonImpl.CoolDownOnGameStart : AmongUsUtil.VanillaKillCoolDown;
            PlayerControl.LocalPlayer.killTimer = killCooldown;
            if (GeneralConfigurations.EmergencyCooldownAtGameStart) AmongUsUtil.SetEmergencyCoolDown(10f, false, false);

            HudManager.Instance.KillButton.SetCoolDown(killCooldown, AmongUsUtil.VanillaKillCoolDown);

            if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Sabotage, out var sabo)) sabo.TryCast<SabotageSystemType>()?.SetInitialSabotageCooldown();
            if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out var door)) door.TryCast<IDoorSystem>()?.SetInitialSabotageCooldown();

            PlayerControl.LocalPlayer.AdjustLighting();
            yield return __instance.CoFadeFullScreen(Color.black, Color.clear, 0.2f, false);
            __instance.FullScreen.transform.localPosition = new Vector3(0f, 0f, -500f);
            __instance.IsIntroDisplayed = false;
            __instance.CrewmatesKilled.gameObject.SetActive(GameManager.Instance.ShowCrewmatesKilled());
            GameManager.Instance.StartGame();
        }
        __result = GetEnumerator().WrapToIl2Cpp();

        return false;
    }

}

[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
class TaskTextPatch
{
    public static void Postfix(TaskPanelBehaviour __instance)
    {
        try
        {
            __instance.taskText.text = GameOperatorManager.Instance?.Run(new PlayerTaskTextLocalEvent(GamePlayer.LocalPlayer, __instance.taskText.text)).Text;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ProgressTracker), nameof(ProgressTracker.Start))]
class ProgressTrackerPatch
{
    public static void Postfix(ProgressTracker __instance)
    {
        __instance.transform.localPosition -= new Vector3(0f, 0f, 25f);//MeetingHudを前に出している分、同じく前に出す。
    }
}
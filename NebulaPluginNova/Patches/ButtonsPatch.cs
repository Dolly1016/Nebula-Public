using MS.Internal.Xml.XPath;
using Nebula.Game.Statistics;
using Virial;
using Virial.Events;
using Virial.Events.Player;

namespace Nebula.Patches;

[HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.Refresh))]
public static class SabotageButtonPatch
{
    static bool Prefix(SabotageButton __instance)
    {
        try
        {
            if (!PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.inVent || !GameManager.Instance.SabotagesEnabled() || PlayerControl.LocalPlayer.petting)
                __instance.SetDisabled();
            else
                __instance.SetEnabled();
        }catch
        {
            __instance.SetDisabled();
        }
        return false;
    }
}

[HarmonyPatch(typeof(AdminButton), nameof(AdminButton.Refresh))]
public static class AdminButtonPatch
{
    static bool Prefix(AdminButton __instance)
    {
        try
        {
            if (!(GameManager.Instance == null))
            {
                LogicGameFlowHnS logicGameFlowHnS = GameManager.Instance.LogicFlow.TryCast<LogicGameFlowHnS>()!;
                if (logicGameFlowHnS != null)
                {
                    __instance.useable = logicGameFlowHnS.SeekerAdminMapEnabled(PlayerControl.LocalPlayer);
                    if (!__instance.useable || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen))
                        __instance.SetDisabled();
                    else
                        __instance.SetEnabled();
                }
            }
        }catch
        {
            __instance.SetDisabled();
        }
        return false;
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool))]
public static class HudActivePatch
{
    internal class HudActiveChangeEvent : Virial.Events.Event
    {
        public HudActiveChangeEvent() { }
    }

    static bool Prefix(HudManager __instance,[HarmonyArgument(0)]PlayerControl localPlayer, [HarmonyArgument(1)]RoleBehaviour role, [HarmonyArgument(2)]bool isActive)
    {
        __instance.UpdateHudContent();

        __instance.UseButton.transform.parent.gameObject.SetActive(isActive);
        __instance.TaskPanel.gameObject.SetActive(isActive);
        __instance.roomTracker.gameObject.SetActive(isActive);
        __instance.joystick?.ToggleVisuals(isActive);
        __instance.ToggleRightJoystick(isActive);

        GameOperatorManager.Instance?.Run(new HudActiveChangeEvent());
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Revive))]
public static class RevivePatch
{
    static bool Prefix(PlayerControl __instance)
    {
        __instance.Data.IsDead = false;
        __instance.gameObject.layer = LayerMask.NameToLayer("Players");
        __instance.MyPhysics.ResetMoveState(true);
        __instance.clickKillCollider.enabled = true;
        __instance.cosmetics.SetPetSource(__instance);
        __instance.cosmetics.SetNameMask(true);
        if (__instance.AmOwner)
        {
            DestroyableSingleton<HudManager>.Instance.ShadowQuad.gameObject.SetActive(true);
            DestroyableSingleton<HudManager>.Instance.SetHudActive(true);
            DestroyableSingleton<HudManager>.Instance.Chat.ForceClosed();
            DestroyableSingleton<HudManager>.Instance.Chat.SetVisible(false);
        }

        return false;
    }
}


[HarmonyPatch(typeof(VentButton), nameof(VentButton.DoClick))]
public static class VentClickPatch
{
    static bool Prefix(VentButton __instance)
    {
        if ((!PlayerControl.LocalPlayer.inVent) && (GamePlayer.LocalPlayer?.Unbox().Role?.VentCoolDown?.IsProgressing ?? false))
            return false;

        if (__instance.currentTarget != null)
        {
            var role = GamePlayer.LocalPlayer!.Role;
        }

        return true;
    }
}

[HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
public static class KillButtonClickPatch
{
    static bool Prefix(KillButton __instance)
    {
        if (__instance.enabled && __instance.currentTarget && !__instance.isCoolingDown && !PlayerControl.LocalPlayer.Data.IsDead && PlayerControl.LocalPlayer.CanMove)
        {
            var cancelable = GameOperatorManager.Instance?.Run(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(GamePlayer.LocalPlayer!, __instance.currentTarget.GetModInfo()!));
            if (!(cancelable?.IsCanceled ?? false))
            {
                //キャンセルされなければキルを実行する
                GamePlayer.LocalPlayer?.MurderPlayer(__instance.currentTarget.GetModInfo()!, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);
            }

            //クールダウンをリセットする
            if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();

            __instance.SetTarget(null);
        }
        return false;
    }
}

[HarmonyPatch(typeof(KillButton), nameof(KillButton.SetTarget))]
public static class KillButtonSetTargetPatch
{
    static bool Prefix(KillButton __instance)
    {
        return __instance.gameObject.active;
    }
}

[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.InitializeAbilityButton))]
class BlockInitializeAbilityButtonPatch
{
    static bool Prefix(RoleBehaviour __instance)
    {
        HudManager.Instance.AbilityButton.gameObject.SetActive(false);
        return false;
    }
}

//アドミンボタンだけつけないように変更
[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.Initialize))]
class BlockInitializePatch
{
    static bool Prefix(RoleBehaviour __instance, [HarmonyArgument(0)] PlayerControl player)
    {
        __instance.Player = player;
        if (!player.AmOwner) return false;
        
        if (__instance.IsImpostor)
        {
            if (__instance.CanUseKillButton)
            {
                DestroyableSingleton<HudManager>.Instance.KillButton.Show();
                if(player.killTimer < 10f) player.SetKillTimer(10f);
            }
            DestroyableSingleton<HudManager>.Instance.SabotageButton.Show();
            DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.Show();
        }
        DestroyableSingleton<HudManager>.Instance.SetHudActive(player, __instance, true);
        PlayerNameColor.SetForRoleDirectly(player, __instance);
        __instance.InitializeAbilityButton();

        return false;
    }
}

[HarmonyPatch(typeof(ReportButton), nameof(ReportButton.DoClick))]
public static class ReportButtonClickPatch
{
    static bool Prefix(ReportButton __instance)
    {
        var info = GamePlayer.LocalPlayer;
        if (info == null) return true;

        if (info.IsDived || info.IsBlown) return false;
        return true;
    }
}

[HarmonyPatch(typeof(UseButton), nameof(UseButton.SetFromSettings))]
class UseButtonSettingsPatch
{
    public static void Postfix(UseButton __instance, [HarmonyArgument(0)] UseButtonSettings settings)
    {
        if((int)settings.Text == ModStringNames.Marketplace)
        {
            __instance.buttonLabelText.text = Language.Translate("button.label.marketplace");
        }
    }
}
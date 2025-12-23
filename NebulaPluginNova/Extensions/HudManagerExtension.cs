using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Roles;
using Virial.Assignable;

namespace Nebula.Extensions;

public static class HudManagerExtension
{
    static public void UpdateHudContent(this HudManager manager)
    {
        manager.UseButton.Refresh();

        if (!PlayerControl.LocalPlayer) return;

        if(NebulaGameManager.Instance?.GameState == NebulaGameStates.NotStarted)
        {
            manager.ReportButton.ToggleVisible(false);
            manager.KillButton.ToggleVisible(false);
            manager.SabotageButton.ToggleVisible(false);
            manager.ImpostorVentButton.ToggleVisible(false);
            return;
        }

        bool flag = PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.IsDead;
        GamePlayer? modPlayer = GamePlayer.LocalPlayer;
        RuntimeRole? modRole = modPlayer?.Role;

        manager.ReportButton.ToggleVisible(!flag && (modRole?.CanReport ?? false) && ShipStatus.Instance != null);
        manager.KillButton.ToggleVisible((modPlayer?.ShowKillButton ?? true) && !flag);
        manager.SabotageButton.ToggleVisible((modRole?.CanInvokeSabotage ?? false));
        manager.ImpostorVentButton.ToggleVisible(!flag && ((modRole?.CanUseVent ?? false) || PlayerControl.LocalPlayer.walkingToVent || PlayerControl.LocalPlayer.inVent));
        manager.MapButton.gameObject.SetActive(NebulaGameManager.Instance.GameMode?.ShowMap ?? true);
    }

    static public void ShowVanillaKeyGuide(this HudManager manager)
    {
#if PC
        //ボタンのガイドを表示
        var keyboardMap = Rewired.ReInput.mapping.GetKeyboardMapInstanceSavedOrDefault(0, 0, 0);
        Il2CppReferenceArray<Rewired.ActionElementMap> actionArray;
        Rewired.ActionElementMap actionMap;

        //マップ
        actionArray = keyboardMap.GetButtonMapsWithAction(4);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideOnVanillaSmallButton(HudManager.Instance.MapButton.gameObject, actionMap.keyCode);
            ButtonEffect.SetKeyGuideForVanillaButton(HudManager.Instance.SabotageButton.gameObject, actionMap.keyCode);
        }
        HudManager.Instance.MapButton.transform.SetLocalZ(-60f);
        if(HudManager.Instance.MapButton.gameObject.TryGetComponent<AspectPosition>(out var aspectPosition))
        {
            var distance = aspectPosition.DistanceFromEdge;
            distance.z = -60f;
            aspectPosition.DistanceFromEdge = distance;
        }

        //使用
        actionArray = keyboardMap.GetButtonMapsWithAction(6);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(HudManager.Instance.UseButton.gameObject, actionMap.keyCode);
            ButtonEffect.SetKeyGuideForVanillaButton(HudManager.Instance.PetButton.gameObject, actionMap.keyCode);
        }

        //レポート
        actionArray = keyboardMap.GetButtonMapsWithAction(7);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(HudManager.Instance.ReportButton.gameObject, actionMap.keyCode);
        }

        //キル
        actionArray = keyboardMap.GetButtonMapsWithAction(8);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(HudManager.Instance.KillButton.gameObject, actionMap.keyCode);
        }

        //ベント
        actionArray = keyboardMap.GetButtonMapsWithAction(50);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(HudManager.Instance.ImpostorVentButton.gameObject, actionMap.keyCode);
        }
#endif
    }
}

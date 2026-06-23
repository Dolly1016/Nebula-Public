using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Roles;
using Virial.Assignable;

namespace Nebula.Extensions;

public static class HudManagerExtension
{
    static public void UpdateHudContent(this HudManager manager)
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;

        NebulaProfiler.LapTimer("Before UpdateHudContent");
        bridge.UseButton.Refresh();
        NebulaProfiler.LapTimer("UseButton.Refresh");

        var localPlayer = AmongUsLLImpl.LocalPlayer;
        if (!localPlayer.AsBoolFast()) return;

        if(NebulaGameManager.Instance?.GameState == NebulaGameStates.NotStarted)
        {
            bridge.ReportButton.ToggleVisible(false);
            bridge.KillButton.ToggleVisible(false);
            bridge.SabotageButton.ToggleVisible(false);
            bridge.ImpostorVentButton.ToggleVisible(false);
            return;
        }

        NebulaProfiler.LapTimer("UpdateHudContent.1");

        var localData = localPlayer.Data;
        bool flag = localData != null && localData.IsDead;
        GamePlayer? modPlayer = GamePlayer.LocalPlayer;
        RuntimeRole? modRole = modPlayer?.Role;

        NebulaProfiler.LapTimer("UpdateHudContent.2");

        bridge.ReportButton.ToggleVisible(!flag && (modRole?.CanReport ?? false) && AmongUsLLImpl.ShipStatusInstance.AsBoolFast());
        bridge.KillButton.ToggleVisible((modPlayer?.ShowKillButton ?? true) && !flag);
        bridge.SabotageButton.ToggleVisible((modRole?.CanInvokeSabotage ?? false));

        NebulaProfiler.LapTimer("UpdateHudContent.3");

        var ventState = GameOperatorManager.Instance?.Run(new Virial.Events.Player.PlayerUpdateVentStateLocalEvent(modPlayer));

        NebulaProfiler.LapTimer("UpdateHudContent.4");

        bridge.ImpostorVentButton.ToggleVisible(!flag && ((ventState?.ShouldShowVentButton ?? false) || localPlayer.walkingToVent || localPlayer.inVent));
        bridge.MapButtonObj.SetActive(NebulaGameManager.Instance.GameMode?.ShowMap ?? true);

        NebulaProfiler.LapTimer("UpdateHudContent.5");
    }

    static public void ShowVanillaKeyGuide(this HudManager manager)
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;
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
            ButtonEffect.SetKeyGuideOnVanillaSmallButton(bridge.MapButtonObj, actionMap.keyCode);
            ButtonEffect.SetKeyGuideForVanillaButton(bridge.SabotageButtonObj, actionMap.keyCode);
        }
        bridge.MapButtonTransform.SetLocalZ(-60f);
        if(bridge.MapButtonObj.TryGetComponent<AspectPosition>(out var aspectPosition))
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
            ButtonEffect.SetKeyGuideForVanillaButton(bridge.UseButtonObj, actionMap.keyCode);
            ButtonEffect.SetKeyGuideForVanillaButton(bridge.PetButtonObj, actionMap.keyCode);
        }

        //レポート
        actionArray = keyboardMap.GetButtonMapsWithAction(7);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(bridge.ReportButtonObj, actionMap.keyCode);
        }

        //キル
        actionArray = keyboardMap.GetButtonMapsWithAction(8);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(bridge.KillButtonObj, actionMap.keyCode);
        }

        //ベント
        actionArray = keyboardMap.GetButtonMapsWithAction(50);
        if (actionArray.Count > 0)
        {
            actionMap = actionArray[0];
            ButtonEffect.SetKeyGuideForVanillaButton(bridge.ImpostorVentButtonObj, actionMap.keyCode);
        }
#endif
    }
}

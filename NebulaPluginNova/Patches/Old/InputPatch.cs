using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

file static class AdditionalConditions
{
    static public bool CanKill => HudManager.Instance.KillButton.gameObject.active;
    static public bool CanUseVent => HudManager.Instance.ImpostorVentButton.gameObject.active;

}
[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.HandleHud))]
public static class KeyboardInputPatch
{
    public static bool Prefix(KeyboardJoystick __instance)
    {
        if (!AmongUsLLImpl.TryGetHudManager(out var hud, out var bridge)) return false;

        if (KeyboardJoystick.player.GetButtonDown(7)) bridge.ReportButton.DoClick();

        if (KeyboardJoystick.player.GetButtonDown(6)) bridge.UseButton.DoClick();

        if (KeyboardJoystick.player.GetButtonDown(4) && !bridge.Chat.IsOpenOrOpening && (NebulaGameManager.Instance?.GameMode?.ShowMap ?? true))
            hud.ToggleMapVisible(GameManager.Instance.GetMapOptions());

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Kill).KeyDown && AdditionalConditions.CanKill) bridge.KillButton.DoClick();
        if (KeyboardJoystick.player.GetButtonDown(50) && bridge.ImpostorVentButtonObj.active) bridge.ImpostorVentButton.DoClick();

        return false;
    }
}

[HarmonyPatch(typeof(ConsoleJoystick), nameof(ConsoleJoystick.HandleHUD))]
public static class ConsoleInputPatch
{
    //非インポスターの場合の処理を後挿しで追加する。
    public static void Postfix(ConsoleJoystick __instance)
    {
        if (ConsoleJoystick.inputState == ConsoleJoystick.ConsoleInputState.Vent)
        {
            if (!ControllerManager.Instance.IsUiControllerActive)
            {
                if (Vent.currentVent)
                {
                    if (!(__instance.delta.sqrMagnitude > 0.25f))
                    {
                        //インポスターでない場合、入力を無視してしまうため、追加
                        var data = AmongUsLLImpl.LocalPlayer.Data;
                        if (data != null && data.Role != null && !data.Role.IsImpostor && ConsoleJoystick.player.GetButtonDown(50) && AdditionalConditions.CanUseVent)
                        {
                            AmongUsLLImpl.HudManagerBridge.ImpostorVentButton.DoClick();
                        }
                    }
                }
            }
        }
        else
        {
            var data = AmongUsLLImpl.LocalPlayer.Data;
            //インポスターでない場合、入力を無視してしまうため、追加
            if (data != null && data.Role != null && !data.Role.IsImpostor)
            {
                if (ConsoleJoystick.player.GetButtonDown(8) && AdditionalConditions.CanKill)
                {
                    AmongUsLLImpl.HudManagerBridge.KillButton.DoClick();
                }
                if (ConsoleJoystick.player.GetButtonDown(50) && AdditionalConditions.CanUseVent)
                {
                    AmongUsLLImpl.HudManagerBridge.ImpostorVentButton.DoClick();
                }
            }
        }
    }
}
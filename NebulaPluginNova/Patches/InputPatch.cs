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
        if (!DestroyableSingleton<HudManager>.InstanceExists) return false;

        if (KeyboardJoystick.player.GetButtonDown(7)) HudManager.Instance.ReportButton.DoClick();

        if (KeyboardJoystick.player.GetButtonDown(6)) HudManager.Instance.UseButton.DoClick();

        if (KeyboardJoystick.player.GetButtonDown(4) && !HudManager.Instance.Chat.IsOpenOrOpening)
            HudManager.Instance.ToggleMapVisible(GameManager.Instance.GetMapOptions());

        if (NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Kill).KeyDown && AdditionalConditions.CanKill) HudManager.Instance.KillButton.DoClick();
        if (KeyboardJoystick.player.GetButtonDown(50) && HudManager.Instance.ImpostorVentButton.gameObject.active) HudManager.Instance.ImpostorVentButton.DoClick();

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
                        if (PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.Role != null && !PlayerControl.LocalPlayer.Data.Role.IsImpostor && ConsoleJoystick.player.GetButtonDown(50) && AdditionalConditions.CanUseVent)
                        {
                            DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
                        }
                    }
                }
            }
        }
        else
        {
            //インポスターでない場合、入力を無視してしまうため、追加
            if (PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.Role != null && !PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                if (ConsoleJoystick.player.GetButtonDown(8) && AdditionalConditions.CanKill)
                {
                    DestroyableSingleton<HudManager>.Instance.KillButton.DoClick();
                }
                if (ConsoleJoystick.player.GetButtonDown(50) && AdditionalConditions.CanUseVent)
                {
                    DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
                }
            }
        }
    }
}
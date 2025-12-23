using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Components;
using Virial.Events.Game;
using static Rewired.Demos.CustomPlatform.MyPlatformControllerExtension;

namespace Nebula.Listeners;

internal partial class NebulaGameEventListeners
{
    private Image fakeMedScanButtonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.FakeMedScanButton.png", 115f);
    void SetUpFakeMedScanButton(GameStartEvent ev)
    {
        if (!GeneralConfigurations.FakeScanOption) return;
        if (!ShipStatus.Instance.MedScanner) return;

        var scanner = ShipStatus.Instance.MedScanner;
        Vector2 scannerPos = scanner.Position;
        ModAbilityButton button = NebulaAPI.Modules.AbilityButton(NebulaAPI.CurrentGame!, GamePlayer.LocalPlayer, false, true, Virial.Compat.VirtualKeyInput.None, null,
            1f, "scan", fakeMedScanButtonImage,
            null,
            unused => !(GamePlayer.LocalPlayer?.Tasks.HasExecutableTasks ?? false) && GamePlayer.LocalPlayer!.TruePosition.Distance(scannerPos) < 1.3f && !Helpers.AnyNonTriggersBetween(scannerPos, GamePlayer.LocalPlayer!.TruePosition, out _));
        button.OnClick = _ =>
        {
            PlayerControl.LocalPlayer.StartCoroutine(CoWalkToScan(scannerPos).WrapToIl2Cpp());
        };


    }

    private IEnumerator CoWalkToScan(Vector2 scanner)
    {
        var vanillaPlayer = GamePlayer.LocalPlayer!.VanillaPlayer;
        vanillaPlayer.moveable = false;
        yield return GamePlayer.LocalPlayer!.VanillaPlayer.MyPhysics.WalkPlayerTo(scanner, 0.001f, 1f, false);
        vanillaPlayer.moveable = true;
    }
}

namespace Nebula.Patches;


[HarmonyPatch(typeof(MedScanMinigame), nameof(MedScanMinigame.FixedUpdate))]
class MedScanMinigameFixedUpdatePatch
{
    static void Prefix(MedScanMinigame __instance)
    {
        __instance.medscan.CurrentUser = PlayerControl.LocalPlayer.PlayerId;
        __instance.medscan.UsersList.Clear();
    }
}

[HarmonyPatch(typeof(MushroomDoorSabotageMinigame),nameof(MushroomDoorSabotageMinigame.Update))]
class DoorMushroomPatch
{
    static void Postfix(MushroomDoorSabotageMinigame __instance)
    {
        if (GeneralConfigurations.FungleQuickPaceDoorMinigameOption)
        {
            foreach (var m in __instance.mushrooms)
            {
                if (m.CurrentState != MushroomDoorSabotageMinigameMushroom.State.Whacked) continue;

                if (m.sprite.color.a < 0.001f)
                {
                    m.StopAllCoroutines();

                    m.transform.localScale = Vector3.zero;
                    m.CurrentState = MushroomDoorSabotageMinigameMushroom.State.Invisible;
                    m.button.SetButtonEnableState(true);
                    m.button.enabled = false;
                    foreach (var renderer in m.GetComponentsInChildren<SpriteRenderer>())
                    {
                        renderer.enabled = false;
                        renderer.enabled = true;
                        renderer.color = Color.white;
                    }
                }
            }
        }
    }
}
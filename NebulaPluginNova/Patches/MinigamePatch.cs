namespace Nebula.Patches;


[HarmonyPatch(typeof(Minigame), nameof(Minigame.Begin))]
class MinigameBeginPatch
{
    static bool Prefix(Minigame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        Minigame.Instance = __instance;
        __instance.MyTask = task;
        __instance.MyNormTask = task ? task.TryCast<NormalPlayerTask>() : null;
        __instance.timeOpened = Time.realtimeSinceStartup;
        if (PlayerControl.LocalPlayer)
        {
            if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
            
            //タスクに吸い寄せられるのを阻止
            PlayerControl.LocalPlayer.NetTransform.HaltSmoothly();
        }
        __instance.StartCoroutine(__instance.CoAnimateOpen());
        return false;
    }
}

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
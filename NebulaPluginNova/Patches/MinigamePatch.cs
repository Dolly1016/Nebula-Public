namespace Nebula.Patches;


[HarmonyPatch(typeof(Minigame), nameof(Minigame.Begin))]
public class MinigameBeginPatch
{
    //サボタージュタスクのキャンセル
    static public bool SabotagePrefix(Minigame __instance, PlayerTask task, params SystemTypes[] mySystemTypes)
    {
        if (task == null) return true;

        var sabTask = task.TryCast<SabotageTask>();
        if (sabTask != null)
        {
            if (NebulaGameManager.Instance?.LocalFakeSabotage?.OnStartMinigame(sabTask) ?? false)
            {
                var o2reactor = task.TryCast<NoOxyTask>()?.reactor;
                if (o2reactor != null) o2reactor.Countdown = 10000f;
                task.TryCast<ReactorTask>()?.reactor.ClearSabotage();
                task.TryCast<HeliCharlesTask>()?.sabotage.ClearSabotage();

                foreach(var type in mySystemTypes) FakeSabotageStatus.RpcRemoveMyFakeSabotage(type);
                //FixedUpdateがあれば更新をかける
                sabTask.GetIl2CppType().GetMethod("FixedUpdate", Il2CppSystem.Reflection.BindingFlags.InvokeMethod | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance)?.Invoke(sabTask, null);

                NebulaAsset.PlaySE(NebulaAudioClip.FakeSabo);

                if (sabTask.IsComplete)
                {
                    GameObject.Destroy(__instance.gameObject);
                    return false;
                }
            }
        }

        return true;
    }

    static bool Prefix(Minigame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!SabotagePrefix(__instance, task)) return false;

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

[HarmonyPatch(typeof(TuneRadioMinigame), nameof(TuneRadioMinigame.Begin))]
class RadioMinigameFixedUpdatePatch
{
    static bool Prefix(TuneRadioMinigame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!MinigameBeginPatch.SabotagePrefix(__instance, task, SystemTypes.Comms)) return false;

        return true;
    }
}

[HarmonyPatch(typeof(SwitchMinigame), nameof(SwitchMinigame.Begin))]
class SwitchMinigameFixedUpdatePatch
{
    static bool Prefix(SwitchMinigame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!MinigameBeginPatch.SabotagePrefix(__instance, task, SystemTypes.Electrical)) return false;
        
        return true;
    }
}

[HarmonyPatch(typeof(ReactorMinigame), nameof(ReactorMinigame.Begin))]
class ReactorMinigameFixedUpdatePatch
{
    static bool Prefix(ReactorMinigame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!MinigameBeginPatch.SabotagePrefix(__instance, task, SystemTypes.Reactor, SystemTypes.Laboratory)) return false;

        return true;
    }
}

[HarmonyPatch(typeof(AuthGame), nameof(AuthGame.Begin))]
class AuthGameFixedUpdatePatch
{
    static bool Prefix(AuthGame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!MinigameBeginPatch.SabotagePrefix(__instance, task, SystemTypes.Comms)) return false;

        return true;
    }
}

[HarmonyPatch(typeof(KeypadGame), nameof(KeypadGame.Begin))]
class KeypadGameFixedUpdatePatch
{
    static bool Prefix(KeypadGame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!MinigameBeginPatch.SabotagePrefix(__instance, task, SystemTypes.LifeSupp)) return false;

        return true;
    }
}

[HarmonyPatch(typeof(AirshipAuthGame), nameof(AirshipAuthGame.Begin))]
class AirshipAuthGameFixedUpdatePatch
{
    static bool Prefix(AirshipAuthGame __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        if (!MinigameBeginPatch.SabotagePrefix(__instance, task,SystemTypes.HeliSabotage)) return false;

        return true;
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
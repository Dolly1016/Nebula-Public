using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Virial.Events.Game.Meeting;

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

        return true;
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
        if (GeneralConfigurations.FungleQuickPaceDoorMinigameOption.CurrentValue)
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

//配線タスクの順番ランダム化
[HarmonyPatch(typeof(NormalPlayerTask), nameof(NormalPlayerTask.PickRandomConsoles),typeof(TaskTypes),typeof(Il2CppStructArray<byte>))]
public static class RandomTaskPatch
{
    static public bool Prefix(NormalPlayerTask __instance, ref Il2CppSystem.Collections.Generic.List<Console> __result, [HarmonyArgument(0)] TaskTypes taskType, [HarmonyArgument(1)] Il2CppStructArray<byte> consoleIds)
    {
        List<Console> orgList = ShipStatus.Instance.AllConsoles.Where((t) => { return t.TaskTypes.Contains(taskType); }).ToList<Console>();
        List<Console> list = new List<Console>(orgList);
        List<Console> result = new List<Console>();

        //返り値を作成する
        __result = new();
        foreach (var console in orgList) __result.Add(console);

        for (int i = 0; i < consoleIds.Length; i++)
        {
            //候補が全て上がってしまったらリセット
            if (list.Count == 0) list = new List<Console>(orgList);
            int index = System.Random.Shared.Next(list.Count);
            result.Add(list[index]);
            list.RemoveAt(index);
        }

        if (!GeneralConfigurations.RandomizedWiringOption) result.Sort((console1, console2) => { return console1.ConsoleId - console2.ConsoleId; });

        //得られた並び順を返す
        for (int i = 0; i < consoleIds.Length; i++) consoleIds[i] = (byte)result[i].ConsoleId;

        return false;
    }
}

//タスクのステップ数改変
[HarmonyPatch(typeof(NormalPlayerTask), nameof(NormalPlayerTask.Initialize))]
public static class TaskStepPatch
{
    public static void Prefix(NormalPlayerTask __instance)
    {
        if (__instance.TaskType == TaskTypes.FixWiring && AmongUsUtil.CurrentMapId != 5) __instance.MaxStep = GeneralConfigurations.StepsOfWiringGameOption;
    }
}

//DefaultレイヤーからUIレイヤーに変更
[HarmonyPatch(typeof(BuildSandcastleMinigame), nameof(BuildSandcastleMinigame.Begin))]
public static class FixSandcastleMinigamePatch
{
    public static void Postfix(BuildSandcastleMinigame __instance)
    {
        __instance.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetUILayer());
    }
}


//緊急会議ボタン
[HarmonyPatch(typeof(EmergencyMinigame), nameof(EmergencyMinigame.Update))]
public static class EmergencyUpdatePatch
{
    private static void WaitingButtonUpdate(EmergencyMinigame __instance, float leftTime)
    {
        //int num = Mathf.CeilToInt(15f - ShipStatus.Instance.Timer);
        //num = Mathf.Max(Mathf.CeilToInt(ShipStatus.Instance.EmergencyCooldown), num);
        int num = Mathf.CeilToInt(leftTime);
        __instance.ButtonActive = false;
        __instance.StatusText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.EmergencyNotReady);
        __instance.NumberText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.SecondsAbbv, num);
        __instance.ClosedLid.gameObject.SetActive(true);
        __instance.OpenLid.gameObject.SetActive(false);
        __instance.state = 0;
    }
    private static void OpenedButtonUpdate(EmergencyMinigame __instance) {
        if (__instance.state == 1) return;
        __instance.state = 1;
        int remainingEmergencies = PlayerControl.LocalPlayer.RemainingEmergencies;
        __instance.StatusText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.EmergencyCount, PlayerControl.LocalPlayer.Data.PlayerName);
        __instance.NumberText.text = remainingEmergencies.ToString();
        __instance.ButtonActive = (remainingEmergencies > 0);
        __instance.ClosedLid.gameObject.SetActive(!__instance.ButtonActive);
        __instance.OpenLid.gameObject.SetActive(__instance.ButtonActive);
    }

    private static void ClosedButtonUpdate(EmergencyMinigame __instance) {
        if (__instance.state == 2) return;
        __instance.state = 2;
        __instance.ButtonActive = false;
        __instance.StatusText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.EmergencyDuringCrisis);
        __instance.NumberText.text = string.Empty;
        __instance.ClosedLid.gameObject.SetActive(true);
        __instance.OpenLid.gameObject.SetActive(false);
    }

    private static void ClosedForModOptionButtonUpdate(EmergencyMinigame __instance, string? translationKey)
    {
        if (__instance.state == 3) return;
        __instance.state = 3;
        __instance.ButtonActive = false;
        __instance.StatusText.text = Language.Translate(translationKey ?? "game.meeting.cannotUseEmergencyButton");
        __instance.NumberText.text = string.Empty;
        __instance.ClosedLid.gameObject.SetActive(true);
        __instance.OpenLid.gameObject.SetActive(false);
    }

    public static bool Prefix(EmergencyMinigame __instance)
    {
        float Cooldown = Mathf.Max(GeneralConfigurations.EmergencyCooldownAtGameStart ? 15f - ShipStatus.Instance.Timer : 0f, ShipStatus.Instance.EmergencyCooldown);

        var checkCanPushEv = GameOperatorManager.Instance!.Run<CheckCanPushEmergencyButtonEvent>(new());

        //クールダウン中はボタンを押せない
        if (Cooldown > 0f)
            WaitingButtonUpdate(__instance, Cooldown);
        //緊急サボタージュが発生している場合はボタンを押せない
        else if (PlayerControl.LocalPlayer.myTasks.GetFastEnumerator().Any(PlayerTask.TaskIsEmergency))
        {
            ClosedButtonUpdate(__instance);
        }
        //フェイクタスクでない緊急タスクがある場合ボタンは押せない
        else if (PlayerControl.LocalPlayer.myTasks.GetFastEnumerator().Any(task => PlayerTask.TaskIsEmergency(task) &&
                (NebulaGameManager.Instance?.LocalFakeSabotage?.MyFakeTasks.All(
                    type => ShipStatus.Instance.GetSabotageTask(type)?.TaskType != task.TaskType) ?? true)))
            ClosedButtonUpdate(__instance);
        else if (!checkCanPushEv.CanPushButton)
            ClosedForModOptionButtonUpdate(__instance, checkCanPushEv.CannotPushReason);
        else
            OpenedButtonUpdate(__instance);

        return false;
    }
}

//子のゲームにConsoleを登録していない問題を修正
[HarmonyPatch(typeof(DivertPowerMetagame), nameof(DivertPowerMetagame.Begin))]
public static class FixDivertPowerMetagamePatch
{
    public static void Postfix(DivertPowerMetagame __instance)
    {
        Minigame.Instance.Console = __instance.Console;
    }
}

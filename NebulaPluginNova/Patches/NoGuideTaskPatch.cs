using Il2CppSystem.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

file static class NoGuideHelpers
{
    static public bool IsNoGuideTask(TaskTypes type)
    {
        switch(type)
        {
            case TaskTypes.FixWiring:
                return GeneralConfigurations.NoGuideWiringOption;
            case TaskTypes.DivertPower:
                return GeneralConfigurations.NoGuideDivertPowerOption;
            case TaskTypes.UploadData:
                return GeneralConfigurations.NoGuideUploadOption;
            case TaskTypes.EmptyGarbage:
                return GeneralConfigurations.NoGuideGarbageOption && (AmongUsUtil.CurrentMapId is 0 or 4);
            case TaskTypes.RoastMarshmallow:
                return GeneralConfigurations.NoGuideMarshmallowOption;
            case TaskTypes.HelpCritter:
                return GeneralConfigurations.NoGuideHelpCritterOption;
            case TaskTypes.ReplaceParts:
                return GeneralConfigurations.NoGuideReplacePartsOption;
            case TaskTypes.CollectSamples:
                return GeneralConfigurations.NoGuideCollectSamplesOption;
            case TaskTypes.SortRecords:
                return GeneralConfigurations.NoGuideSortRecordsOption;
        }
        return false;
    }

    static public bool IsFakeConsole(NormalPlayerTask task, TaskTypes type, Console console)
    {
        switch (type)
        {
            case TaskTypes.UploadData:
            case TaskTypes.DivertPower:
                return console.ValidTasks.Any(set => set.taskType == type);
            case TaskTypes.FixWiring:
                return console.TaskTypes.Contains(TaskTypes.FixWiring);
            case TaskTypes.EmptyGarbage:
            case TaskTypes.HelpCritter:
            case TaskTypes.CollectSamples:
            case TaskTypes.ReplaceParts:
                return console.ValidTasks.Any(set => set.taskType == type && set.taskStep.Contains(0));
            case TaskTypes.RoastMarshmallow:
            case TaskTypes.SortRecords:
                return console.TaskTypes.Contains(type) && console.ConsoleId == 0;
        }
        return false;
    }

    static public bool IsInFakeStep(NormalPlayerTask task, TaskTypes type)
    {
        switch (type)
        {
            case TaskTypes.FixWiring:
            case TaskTypes.DivertPower:
                return task.TaskStep > 0;
            case TaskTypes.UploadData:
            case TaskTypes.EmptyGarbage:
            case TaskTypes.RoastMarshmallow:
            case TaskTypes.HelpCritter:
            case TaskTypes.CollectSamples:
            case TaskTypes.ReplaceParts:
                return task.TaskStep == 0;
            case TaskTypes.SortRecords:
                return task.Data.Any(b => b != 0 && b != byte.MaxValue);
        }
        return false;
    }

    public static string GetVanillaLikeText(NormalPlayerTask task, TaskTypes type, bool shouldYellow, bool showRoom, string? taskText)
    {
        System.Text.StringBuilder sb = new();
        if (shouldYellow) sb.Append("<color=#FFFF00FF>");

        if (showRoom)
        {
            sb.Append(DestroyableSingleton<TranslationController>.Instance.GetString(task.StartAt));
            sb.Append(": ");
        }
        else
        {
            sb.Append(Language.Translate("task.unknownPlace") + ": ");
        }

        sb.Append(taskText ?? DestroyableSingleton<TranslationController>.Instance.GetString(type));

        if (task.ShowTaskStep)
        {
            sb.Append(" (");
            sb.Append(task.taskStep);
            sb.Append('/');
            sb.Append(task.MaxStep);
            sb.Append(')');
        }
        if (shouldYellow) sb.Append("</color>");

        return sb.ToString();
    }

    static public string? GetFakeTaskText(NormalPlayerTask task, TaskTypes type)
    {
        switch (type)
        {
            case TaskTypes.SortRecords:
            case TaskTypes.HelpCritter:
            case TaskTypes.CollectSamples:
            case TaskTypes.RoastMarshmallow:
                return GetVanillaLikeText(task, type, task.ShouldYellowText(), true, null);
            case TaskTypes.FixWiring:
            case TaskTypes.ReplaceParts:
            case TaskTypes.EmptyGarbage:
            case TaskTypes.UploadData:
                return GetVanillaLikeText(task, type, task.ShouldYellowText(), false, null);
            case TaskTypes.DivertPower:
                return GetVanillaLikeText(task, type, task.ShouldYellowText(), false, TranslationController.Instance.GetString(StringNames.AcceptDivertedPower));
        }
        return null;
    }

    static public IReadOnlyList<Vector2> FindFakeConsolePos(NormalPlayerTask task,TaskTypes taskType)
    {
        task.LocationDirty = false;
        List<Vector2> list = [];
        Console[] allConsoles = ShipStatus.Instance.AllConsoles;
        for (int i = 0; i < allConsoles.Length; i++)
        {
            var console = allConsoles[i];
            if(IsFakeConsole(task, taskType, allConsoles[i]))
            {
                list.Add(console.transform.position);
            }
        }

        //配線タスクはここで部屋を更新しているので更新。
        if(taskType == TaskTypes.FixWiring)
        {
            Console console6 = task.FindSpecialConsole((Func<Console, bool>)(c => c.TaskTypes.Contains(TaskTypes.FixWiring) && c.ConsoleId == task.Data[task.taskStep]));
            task.StartAt = console6.Room;
        }

        return list;
    }

    public static bool AppendTaskTextPrefix(NormalPlayerTask __instance, Il2CppSystem.Text.StringBuilder sb)
    {
        if (__instance.IsComplete) return true;

        var type = __instance.TaskType;
        if (NoGuideHelpers.IsNoGuideTask(type) && NoGuideHelpers.IsInFakeStep(__instance, type))
        {
            var taskText = NoGuideHelpers.GetFakeTaskText(__instance, type);
            if (taskText != null)
            {
                sb.AppendLine(taskText);
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(MapTaskOverlay), nameof(MapTaskOverlay.SetIconLocation))]
internal static class NoGuideOverlayPatch
{
    public static bool Prefix(MapTaskOverlay __instance, [HarmonyArgument(0)] PlayerTask task)
    {
        var type = task.TaskType;
        if (NoGuideHelpers.IsNoGuideTask(type) && NoGuideHelpers.IsInFakeStep(task.CastFast<NormalPlayerTask>(), type))
        {
            var locations = NoGuideHelpers.FindFakeConsolePos(task.CastFast<NormalPlayerTask>(), type);
            for (int i = 0; i < locations.Count; i++)
            {
                Vector3 vector = locations[i] / ShipStatus.Instance.MapScale;
                vector.z = -1f;
                PooledMapIcon pooledMapIcon = __instance.icons.Get<PooledMapIcon>();
                pooledMapIcon.transform.localScale = new Vector3(pooledMapIcon.NormalSize, pooledMapIcon.NormalSize, pooledMapIcon.NormalSize);
                pooledMapIcon.rend.color = Color.green;
                pooledMapIcon.name = task.name;
                pooledMapIcon.lastMapTaskStep = task.TaskStep;
                pooledMapIcon.transform.localPosition = vector;
                
                pooledMapIcon.alphaPulse.enabled = false;
                pooledMapIcon.rend.material.SetFloat("_Outline", 1f);
                
                string text = task.name;
                text += i.ToString();
                if (!__instance.data.ContainsKey(text)) __instance.data.Add(text, pooledMapIcon);
            }

            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(NormalPlayerTask), nameof(NormalPlayerTask.UpdateArrowAndLocation))]
internal static class NoGuideArrowPatch
{
    public static bool Prefix(NormalPlayerTask __instance)
    {
        if (!__instance.Arrow) return false;
        if (!__instance.Owner.AmOwner || __instance.IsComplete)
        {
            __instance.Arrow.gameObject.SetActive(false);
            return false;
        }

        var type = __instance.TaskType;
        if (NoGuideHelpers.IsNoGuideTask(type) && NoGuideHelpers.IsInFakeStep(__instance, type))
        {
            __instance.Arrow.gameObject.SetActive(false);
            __instance.arrowSuspended = false;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(NormalPlayerTask), nameof(NormalPlayerTask.AppendTaskText))]
internal static class NoGuideTaskTextPatch
{
    public static bool Prefix(NormalPlayerTask __instance, [HarmonyArgument(0)] Il2CppSystem.Text.StringBuilder sb) => NoGuideHelpers.AppendTaskTextPrefix(__instance, sb);
}

[HarmonyPatch(typeof(AirshipUploadTask), nameof(AirshipUploadTask.AppendTaskText))]
internal static class NoGuideTaskTextAirshipUploadPatch
{
    public static bool Prefix(NormalPlayerTask __instance, [HarmonyArgument(0)] Il2CppSystem.Text.StringBuilder sb) => NoGuideHelpers.AppendTaskTextPrefix(__instance, sb);
}

[HarmonyPatch(typeof(DivertPowerTask), nameof(DivertPowerTask.AppendTaskText))]
internal static class NoGuideTaskTextDivertPowerPatch
{
    public static bool Prefix(NormalPlayerTask __instance, [HarmonyArgument(0)] Il2CppSystem.Text.StringBuilder sb)
    {
        if(__instance.taskStep == 0 && NoGuideHelpers.IsNoGuideTask(TaskTypes.DivertPower))
        {
            sb.AppendLine(NoGuideHelpers.GetVanillaLikeText(__instance, TaskTypes.DivertPower, false, true, Language.Translate("task.divertPower")));
            return false;
        }
        return NoGuideHelpers.AppendTaskTextPrefix(__instance, sb);
    }
}

[HarmonyPatch(typeof(DivertPowerMinigame), nameof(DivertPowerMinigame.Begin))]
internal static class NoGuideTaskTextDivertPowerMinigamePatch
{
    public static void Prefix(DivertPowerMinigame __instance)
    {
        if (NoGuideHelpers.IsNoGuideTask(TaskTypes.DivertPower))
        {
            __instance.SliderOrder = __instance.SliderOrder.Shuffle();
        }
    }
}

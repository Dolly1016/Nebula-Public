using Nebula.Patches;
using Nebula.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial.Events.Game;

namespace Nebula.Listeners;

internal partial class NebulaGameEventListeners
{
    TaskBarMode taskBarMode = TaskBarMode.Invisible;
    ProgressTracker progressBar = null!;
    TextMeshPro progressBarText = null!;
    AspectPosition progressBarAspectPosition = null!;
    string vanillaProgressBarText = null!;
    int tasksPerPlayers = 10;
    void CheckTaskBarMode(GameStartEvent ev) {
        taskBarMode = GameManager.Instance.LogicOptions.GetTaskBarMode();
        tasksPerPlayers = AmongUsUtil.NumOfAllTasks;
    }
    void NonCrewmateCanSeeTaskTracker(GameHudUpdateEvent ev)
    {
        if (!progressBar && HudManager.InstanceExists) {
            progressBar = HudManager.Instance.TaskStuff.transform.GetChild(1).GetComponent<ProgressTracker>();
            if (progressBar)
            {
                progressBarText = progressBar.transform.GetChild(2).GetComponent<TextMeshPro>();
                progressBarAspectPosition = progressBar.GetComponent<AspectPosition>();
                progressBarAspectPosition.updateAlways = true;
            }
        }
        if (progressBar)
        {
            bool shouldShow = true;
            bool asNonCrew = false;
            if (NebulaGameManager.Instance?.GameState == NebulaGameStates.NotStarted) shouldShow = false;
            else
            {
                GamePlayer? localPlayer = GamePlayer.LocalPlayer;
                if(localPlayer == null) shouldShow = false;
                else
                {
                    shouldShow = taskBarMode switch
                    {
                        TaskBarMode.Normal => true,
                        TaskBarMode.Invisible => false,
                        TaskBarMode.MeetingOnly => MeetingHud.Instance,
                        _ => false
                    };

                    if (!shouldShow && MeetingHud.Instance)
                    {
                        shouldShow = GeneralConfigurations.NonCrewmateCanSeeTaskTrackerOption.GetValue() switch
                        {
                            1 => localPlayer.IsImpostor,
                            2 => !localPlayer.FeelBeTrueCrewmate,
                            _ => false
                        };
                        asNonCrew = shouldShow;
                    }
                }
                
            }
            
            progressBar.gameObject.SetActive(shouldShow);
            if (shouldShow && progressBarText)
            {
                if (asNonCrew)
                {
                    progressBarText.text = Language.Translate("metaInfo.meeting.task") + ((int)(progressBar.curValue * (float)tasksPerPlayers + 0.5f)).ToString();
                }
                else
                {
                    if (vanillaProgressBarText == null) vanillaProgressBarText = TranslationController.Instance.GetString(StringNames.TotalTasksCompleted);
                    progressBarText.text = vanillaProgressBarText;
                }
            }
            if (shouldShow)
            {
                if (MeetingHud.Instance)
                {
                    progressBarAspectPosition.DistanceFromEdge = new(1.81f, 0.18f, -30f);
                    progressBarAspectPosition.transform.localScale = new(0.45f, 0.45f, 1f);
                }
                else
                {
                    progressBarAspectPosition.DistanceFromEdge = new(2.46f, 0.3f, -5f);
                    progressBarAspectPosition.transform.localScale = new(0.6f, 0.6f, 1f);
                }
            }
            
        }
    }
}

[HarmonyPatch(typeof(ProgressTracker), nameof(ProgressTracker.FixedUpdate))]
public static class AirshipExileWrapUpAnimatePatch
{
    static bool Prefix(ProgressTracker __instance)
    {
        if (AmongUsUtil.InCommSab)
        {
            __instance.TileParent.enabled = false;
            return false;
        }

        if (!__instance.TileParent.enabled) __instance.TileParent.enabled = true;

        CrewmateGameRule.GetCurrentTaskState(out var quota, out var completed);
        if (quota == 0) quota += 1;

        float num2 = (float)completed / (float)quota;
        __instance.curValue = Mathf.Lerp(__instance.curValue, num2, Time.fixedDeltaTime * 2f);
        __instance.TileParent.material.SetFloat("_Buckets", (float)1f);
        __instance.TileParent.material.SetFloat("_FullBuckets", __instance.curValue);

        return false;
    }
}

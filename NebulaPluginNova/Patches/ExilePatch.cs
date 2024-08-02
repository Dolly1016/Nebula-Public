using Nebula.Behaviour;
using Nebula.Game.Statistics;
using Virial;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Helpers;

namespace Nebula.Patches;

public static class ModPreSpawnInPatch
{
    public static IEnumerator ModPreSpawnIn(Transform minigameParent, GameStatistics.EventVariation eventVariation,TranslatableTag tag)
    {
        if (NebulaPreSpawnMinigame.PreSpawnLocations.Length > 0)
        {
            NebulaPreSpawnMinigame spawnInMinigame = UnityHelper.CreateObject<NebulaPreSpawnMinigame>("PreSpawnInMinigame", minigameParent, new Vector3(0, 0, -600f), LayerExpansion.GetUILayer());
            spawnInMinigame.Begin(null!);
            yield return NebulaAPI.CurrentGame.GetModule<Synchronizer>()?.CoSync(Modules.SynchronizeTag.PreSpawnMinigame, true, false, false);
            NebulaAPI.CurrentGame.GetModule<Synchronizer>()?.ResetSync(Modules.SynchronizeTag.PreSpawnMinigame);
            spawnInMinigame.CloseSpawnInMinigame();

            NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(eventVariation, null, 0, GameStatisticsGatherTag.Spawn) { RelatedTag = tag });
        }
        else
        {
            NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(eventVariation, null, 0) { RelatedTag = tag });
        }
    }
}
public static class NebulaExileWrapUp
{
    static public IEnumerator WrapUpAndSpawn(ExileController __instance)
    {
        if ((MeetingHudExtension.ExiledAll?.Length ?? 0) > 0)
        {
            using (RPCRouter.CreateSection("ExilePlayer"))
            {
                foreach (var exiled in MeetingHudExtension.ExiledAll!)
                {
                    if (exiled)
                    {
                        exiled.Exiled();
                        exiled.Data.IsDead = true;
                    }

                    NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Exile, null, 1 << exiled.PlayerId, GameStatisticsGatherTag.Spawn) { RelatedTag = EventDetail.Exiled });

                    var info = exiled.GetModInfo();

                    if (info != null)
                    {
                        info.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
                        info.Unbox().MyState = PlayerState.Exiled;
                        if (info.AmOwner && NebulaAchievementManager.GetRecord("death." + info.PlayerState.TranslationKey, out var rec)) new StaticAchievementToken(rec);

                        //Entityイベント発火
                        GameOperatorManager.Instance?.Run(new PlayerExiledEvent(info), true);
                    }
                }

                NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.SendSync(SynchronizeTag.CheckExtraVictims);
            }

            yield return NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.CoSyncAndReset(Modules.SynchronizeTag.CheckExtraVictims, true, true, false);

            bool extraExile = MeetingHudExtension.ExtraVictims.Count > 0;
            MeetingHudExtension.ExileExtraVictims();

            //誰かが追加でいなくなったとき
            if (GeneralConfigurations.NoticeExtraVictimsOption && extraExile)
            {
                string str = Language.Translate("game.meeting.someoneDisappeared");
                int num = 0;
                var additionalText = GameObject.Instantiate(__instance.Text, __instance.transform);
                additionalText.transform.localPosition = new Vector3(0, 0, -800f);
                additionalText.text = "";

                while (num < str.Length)
                {
                    num++;
                    additionalText.text = str.Substring(0, num);
                    SoundManager.Instance.PlaySoundImmediate(__instance.TextSound, false, 0.8f, 0.92f);
                    yield return new WaitForSeconds(Mathf.Min(2.8f / str.Length, 0.28f));
                }
                yield return new WaitForSeconds(1.9f);

                float a = 1f;
                while (a > 0f)
                {
                    a -= Time.deltaTime * 1.5f;
                    additionalText.color = Color.white.AlphaMultiplied(a);
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
            }
        }

        yield return GameOperatorManager.Instance?.Run(new MeetingPreEndEvent()).Coroutines.WaitAll();
        
        NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.SendSync(SynchronizeTag.PostMeeting);
        yield return NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.CoSyncAndReset(Modules.SynchronizeTag.PostMeeting, true, true, false);

        NebulaGameManager.Instance?.OnMeetingEnd(MeetingHudExtension.ExiledAll);
        GamePlayer[] exiledArray = MeetingHudExtension.ExiledAll?.Select(p => p.GetModInfo()!).ToArray() ?? new GamePlayer[0];
        GameOperatorManager.Instance?.Run(new MeetingEndEvent(exiledArray));

        yield return ModPreSpawnInPatch.ModPreSpawnIn(__instance.transform.parent, GameStatistics.EventVariation.MeetingEnd, EventDetail.MeetingEnd);



        GameOperatorManager.Instance?.Run(new TaskPhaseRestartEvent());

        __instance.ReEnableGameplay();
        AmongUsUtil.SetEmergencyCoolDown(0f, true);

        GameObject.Destroy(__instance.gameObject);
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
public static class ExileWrapUpPatch
{
    static bool Prefix(ExileController __instance)
    {
        __instance.StartCoroutine(NebulaExileWrapUp.WrapUpAndSpawn(__instance).WrapToIl2Cpp());
        return false;
    }
}

[HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.WrapUpAndSpawn))]
public static class AirshipExileWrapUpPatch
{
    static bool Prefix(AirshipExileController __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = NebulaExileWrapUp.WrapUpAndSpawn(__instance).WrapToIl2Cpp();
        return false;
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
class ExileControllerBeginPatch
{
    public static void Prefix(ExileController __instance, [HarmonyArgument(0)] ref NetworkedPlayerInfo exiled, [HarmonyArgument(1)] ref bool tie)
    {
        exiled = MeetingHudExtension.ExiledAll?.FirstOrDefault()?.Data!;
        tie = MeetingHudExtension.WasTie;

        Debug.Log("Rewrite Exiled: " + (exiled?.PlayerName ?? "None"));
    }

    public static void Postfix(ExileController __instance, [HarmonyArgument(0)] ref NetworkedPlayerInfo exiled, [HarmonyArgument(1)] bool tie)
    {
        //MeetingHudがなぜか真になってしまうので、nullに書き換え
        MeetingHud.Instance = null;

        if (exiled == null) return;

        if (MeetingHudExtension.IsObvious)
        {
            __instance.completeString = Language.Translate("game.meeting.obvious");
        }
        else if((MeetingHudExtension.ExiledAll?.Length ?? 0) > 1)
        {
            __instance.completeString = Language.Translate("game.meeting.multiple");
        }
        else if (GeneralConfigurations.ShowRoleOfExiled)
        {
            var role = NebulaGameManager.Instance.GetPlayer(exiled.PlayerId)?.Role;
            if (role != null)
            {
                __instance.completeString = Language.Translate("game.meeting.roleText").Replace("%PLAYER%", exiled.PlayerName).Replace("%ROLE%", role.Role.DisplayName);
                if (role.Role == Roles.Neutral.Jester.MyRole) __instance.ImpostorText.text = Language.Translate("game.meeting.roleJesterText");
            }
        }
    }
}

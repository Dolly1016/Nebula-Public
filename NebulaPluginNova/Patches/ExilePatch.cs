﻿using HarmonyLib;
using Nebula.Behaviour;
using Nebula.Configuration;
using Nebula.Events;
using Nebula.Game;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial.Events.Meeting;
using Virial.Events.Player;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Patches;

public static class ModPreSpawnInPatch
{
    public static IEnumerator ModPreSpawnIn(Transform minigameParent, GameStatistics.EventVariation eventVariation,TranslatableTag tag)
    {
        if (NebulaPreSpawnMinigame.PreSpawnLocations.Length > 0)
        {
            NebulaPreSpawnMinigame spawnInMinigame = UnityHelper.CreateObject<NebulaPreSpawnMinigame>("PreSpawnInMinigame", minigameParent, new Vector3(0, 0, -600f), LayerExpansion.GetUILayer());
            spawnInMinigame.Begin(null!);
            yield return NebulaGameManager.Instance?.Syncronizer.CoSync(Modules.SynchronizeTag.PreSpawnMinigame, true, false, false);
            NebulaGameManager.Instance?.Syncronizer.ResetSync(Modules.SynchronizeTag.PreSpawnMinigame);
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
        PlayerControl? @object = null;
        if (__instance.exiled != null)
        {
            @object = __instance.exiled.Object;
            if (@object) @object.Exiled();
            __instance.exiled.IsDead = true;
            NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Exile, null, 1 << __instance.exiled.PlayerId, GameStatisticsGatherTag.Spawn) { RelatedTag = EventDetail.Exiled });

            var info = @object.GetModInfo();
            if (info != null)
            {
                info.DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
                info.MyState = PlayerState.Exiled;
                if (info.AmOwner && NebulaAchievementManager.GetRecord("death." + info.MyState.TranslationKey, out var rec)) new StaticAchievementToken(rec);

                using (RPCRouter.CreateSection("ExilePlayer"))
                {
                    //Entityイベント発火
                    GameEntityManager.Instance?.GetPlayerEntities(info.PlayerId).Do(e =>
                    {
                        e.OnExiled();
                        e.OnDead();
                    });

                    //APIイベント発火
                    EventManager.HandleEvent(new PlayerDeadEvent(info));

                    //Entityイベント発火
                    GameEntityManager.Instance?.AllEntities.Do(e =>
                    {
                        e.OnPlayerExiled(info);
                        e.OnPlayerDead(info);
                    });

                    var checkEvent = EventManager.HandleEvent(new CheckExtraVictimEvent(info));
                    foreach (var victim in checkEvent.ExtraVictim) victim.victim.VanillaPlayer.ModMarkAsExtraVictim(victim.killer?.VanillaPlayer, victim.reason, victim.eventDetail);

                    NebulaGameManager.Instance.Syncronizer.SendSync(SynchronizeTag.CheckExtraVictims);
                }

                yield return NebulaGameManager.Instance.Syncronizer.CoSyncAndReset(Modules.SynchronizeTag.CheckExtraVictims, true, true, false);
            }

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

        yield return NebulaGameManager.Instance?.AllPlayerInfo().Select(p => p.Role?.CoMeetingEnd()).WaitAll();
        NebulaGameManager.Instance!.Syncronizer.SendSync(SynchronizeTag.PostMeeting);
        yield return NebulaGameManager.Instance!.Syncronizer.CoSyncAndReset(Modules.SynchronizeTag.PostMeeting, true, true, false);

        NebulaGameManager.Instance?.OnMeetingEnd(__instance.exiled?.Object);
        GameEntityManager.Instance?.AllEntities.Do(e => e.OnMeetingEnd());

        yield return ModPreSpawnInPatch.ModPreSpawnIn(__instance.transform.parent, GameStatistics.EventVariation.MeetingEnd, EventDetail.MeetingEnd);



        GameEntityManager.Instance?.AllEntities.Do(e => e.OnGameReenabled());

        __instance.ReEnableGameplay();
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

    public static void Postfix(ExileController __instance, [HarmonyArgument(0)] ref GameData.PlayerInfo exiled, [HarmonyArgument(1)] bool tie)
    {
        if (exiled == null) return;

        if (GeneralConfigurations.ShowRoleOfExiled)
        {
            var role = NebulaGameManager.Instance.GetModPlayerInfo(exiled.PlayerId)?.Role;
            if (role != null)
            {
                __instance.completeString = Language.Translate("game.meeting.roleText").Replace("%PLAYER%", exiled.PlayerName).Replace("%ROLE%", role.Role.DisplayName);
                if (role.Role == Roles.Neutral.Jester.MyRole) __instance.ImpostorText.text = Language.Translate("game.meeting.roleJesterText");
            }
        }
    }
}

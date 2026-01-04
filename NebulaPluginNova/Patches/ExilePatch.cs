using Epic.OnlineServices.Presence;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Neutral;
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
        using (RPCRouter.CreateSection("ExilePlayer"))
        {
            GameOperatorManager.Instance?.Run(new MeetingPreSyncEvent(), true);

            if ((MeetingHudExtension.ExiledAllModCache?.Length ?? 0) > 0)
            {
                foreach (var exiled in MeetingHudExtension.ExiledAllModCache!)
                {
                    if (exiled.VanillaPlayer) exiled.VanillaPlayer.Exiled();
                    var vanillaInfo = GameData.Instance.GetPlayerById(exiled.PlayerId);
                    if(vanillaInfo) vanillaInfo.IsDead = true;
                    if(exiled.VanillaPlayer) PlayerExtension.ResetOnDying(exiled.VanillaPlayer);

                    NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Exile, null, 1 << exiled.PlayerId, GameStatisticsGatherTag.Spawn) { RelatedTag = EventDetail.Exiled });

                    var info = exiled;

                    if (info != null)
                    {
                        info.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
                        info.Unbox().MyState = PlayerState.Exiled;
                        if (info.AmOwner && NebulaAchievementManager.GetRecord("death." + info.PlayerState.TranslationKey, out var rec)) new StaticAchievementToken(rec);
                        if(info.AmOwner) new StaticAchievementToken("stats.death." + info.PlayerState.TranslationKey);

                        //Entityイベント発火
                        GameOperatorManager.Instance?.Run(new PlayerExiledEvent(info), true);
                    }
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
                yield return new WaitForSeconds(Mathn.Min(2.8f / str.Length, 0.28f));
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

        yield return GameOperatorManager.Instance?.Run(new MeetingPreEndEvent(), shouldNotCheckGameEnd: false).Coroutines.WaitAll();
        
        NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.SendSync(SynchronizeTag.PostMeeting);
        yield return NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.CoSyncAndReset(Modules.SynchronizeTag.PostMeeting, true, true, false);

        NebulaGameManager.Instance?.OnMeetingEnd(MeetingHudExtension.ExiledAllModCache);
        GamePlayer[] exiledArray = MeetingHudExtension.ExiledAllModCache?.ToArray() ?? new GamePlayer[0];
        GameOperatorManager.Instance?.Run(new MeetingEndEvent(exiledArray), shouldNotCheckGameEnd: false);

        NebulaGameManager.Instance?.AllPlayerInfo.Do(p => p.VanillaPlayer.MyPhysics.DoingCustomAnimation = false);

        yield return ModPreSpawnInPatch.ModPreSpawnIn(__instance.transform.parent, GameStatistics.EventVariation.MeetingEnd, EventDetail.MeetingEnd);



        GameOperatorManager.Instance?.Run(new TaskPhaseRestartEvent(NebulaGameManager.Instance!), true);

        __instance.ReEnableGameplay();
        AmongUsUtil.SetEmergencyCoolDown(0f, true);

        GameObject.Destroy(__instance.gameObject);

        Resources.UnloadUnusedAssets();
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

[HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.Animate))]
public static class AirshipExileWrapUpAnimatePatch
{
    static void Postfix(AirshipExileController __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var orig = __result;
        IEnumerator CoWrap()
        {
            while (orig.MoveNext())
            {
                var current = orig.Current;
                if (current != null && current?.TryCast<AirshipExileController._WrapUpAndSpawn_d__11>() != null)
                {
                    yield return NebulaExileWrapUp.WrapUpAndSpawn(__instance).WrapToIl2Cpp();
                }
                else
                {
                    yield return current;
                }
            }
        }

        __result = CoWrap().WrapToIl2Cpp();
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
class ExileControllerBeginPatch
{
    public static void Prefix(ExileController __instance, [HarmonyArgument(0)] ref ExileController.InitProperties init)
    {
        init.voteTie = MeetingHudExtension.WasTie;
        var first = MeetingHudExtension.ExiledAllModCache?.FirstOrDefault();
        init.networkedPlayer = GameData.Instance.GetPlayerById(first?.PlayerId ?? byte.MaxValue);
        init.outfit = first?.DefaultOutfit.outfit;
        init.isImpostor = first?.IsImpostor ?? false;

        StampHelpers.SetStampShowerToUnderHud(() => ExileController.Instance);
    }

    public static void Postfix(ExileController __instance, [HarmonyArgument(0)] ref ExileController.InitProperties init)
    {
        GameOperatorManager.Instance?.Run(new ExileSceneStartEvent(MeetingHudExtension.ExiledAllModCache!));

        bool impostorTextShouldNotBeChanged = false;
        if (init.networkedPlayer != null)
        {

            if (MeetingHudExtension.IsObvious)
            {
                __instance.completeString = Language.Translate("game.meeting.obvious");
            }
            else if ((MeetingHudExtension.ExiledAllModCache?.Length ?? 0) > 1)
            {
                __instance.completeString = Language.Translate("game.meeting.multiple");
            }
            else if (GeneralConfigurations.ShowRoleOfExiled && AmongUsUtil.GetCurrentNormalOption().ConfirmImpostor)
            {
                var role = NebulaGameManager.Instance.GetPlayer(init.networkedPlayer.PlayerId)?.Role;
                if (role != null)
                {
                    __instance.completeString = Language.Translate("game.meeting.roleText").Replace("%PLAYER%", init.networkedPlayer.PlayerName).Replace("%ROLE%", role.Role.DisplayName);
                    if (role.Role == Roles.Neutral.Jester.MyRole)
                    {
                        __instance.ImpostorText.text = Language.Translate("game.meeting.roleJesterText");
                        impostorTextShouldNotBeChanged = true;
                    }
                }
            }
        }

        var texts = GameOperatorManager.Instance?.Run(new FixExileTextEvent(MeetingHudExtension.ExiledAllModCache!)).GetTexts();

        var impostorText = __instance.ImpostorText;
        impostorText.rectTransform.pivot = new(0.5f, 1f);
        impostorText.rectTransform.sizeDelta = new(11.555f, 2f);
        impostorText.alignment = TMPro.TextAlignmentOptions.Top;

        //追放を確認の詳細設定
        if (init.confirmImpostor && !impostorTextShouldNotBeChanged)
        {
            var confirmText = impostorText.text;
            int num = 0;
            int exiledBitFlag = 0;
            foreach (var exiled in MeetingHudExtension.ExiledAllModCache ?? []) exiledBitFlag |= 1 << exiled.PlayerId;
            bool IsDead(GamePlayer p) => p.IsDead || (exiledBitFlag & (1 << p.PlayerId)) != 0;
            switch (GeneralConfigurations.ConfirmEjectTargetOption.GetValue())
            {
                case 0: //インポスター陣営
                    num = GamePlayer.AllPlayers.Count(p => !IsDead(p) && p.IsImpostor);
                    confirmText = Language.Translate(num > 1 ? "exile.confirmEject.impostor.p" : "exile.confirmEject.impostor.s").Replace("%NUM%", num.ToString());
                    break;
                case 1: //キラー陣営
                    num = GamePlayer.AllPlayers.Count(p => !IsDead(p) && p.Role.Role.IsKiller);
                    confirmText = Language.Translate(num > 1 ? "exile.confirmEject.killer.p" : "exile.confirmEject.killer.s").Replace("%NUM%", num.ToString());
                    break;
                case 2: //全人外
                    num = GamePlayer.AllPlayers.Count(p => !IsDead(p) && !p.IsCrewmate);
                    confirmText = Language.Translate(num > 1 ? "exile.confirmEject.nonCrewmate.p" : "exile.confirmEject.nonCrewmate.s").Replace("%NUM%", num.ToString());
                    break;
            }

            var localPlayer = GamePlayer.LocalPlayer;
            bool canSeeText = GeneralConfigurations.ConfirmEjectConditionOption.GetValue() switch {
                0 => true,
                1 => localPlayer.IsImpostor,
                2 => localPlayer.Role.Role.IsKiller,
                3 => !localPlayer.IsCrewmate
            };
            if (!canSeeText) confirmText = "";
            impostorText.text = confirmText;
        }

        if (texts != null && texts.Count > 0)
        {
            if (!init.confirmImpostor) impostorText.text = "";
            init.confirmImpostor = true;

            impostorText.rectTransform.anchoredPosition3D += new Vector3(0f, 0.1f, 0f);

            var text = impostorText.text;
            text = "<line-height=90%>" + text;
            texts.Do(str => text += "<br>" + str);
            impostorText.text = text;
        }
        else
        {
            impostorText.rectTransform.anchoredPosition3D += new Vector3(0f, 0.19f, 0f);
        }
        
    }
}

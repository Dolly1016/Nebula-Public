using Nebula.Game.Statistics;
using Nebula.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Game;

[NebulaRPCHolder]
internal class KillRequestHandler
{
    record KillRequest(int Id, GamePlayer Killer, GamePlayer Target, CommunicableTextTag PlayerState, CommunicableTextTag? RecordState, KillParameter KillParam, KillCondition KillCondition, Action<KillResult>? CallBack);

    private int availableId = 0;
    private List<KillRequest> myRequests = new();

    private static uint PackFlags(KillParameter killParam, KillCondition killCondition) => (uint)killParam | ((uint)killCondition << 16);
    private static void UnpackFlags(uint flag, out KillParameter killParam, out KillCondition killCondition)
    {
        killParam = (KillParameter)(flag & 0xFFFF);
        killCondition = (KillCondition)((flag >> 16) & 0xFFFF);
    } 
    public void RequestKill(GamePlayer killer, GamePlayer target, CommunicableTextTag playerState, CommunicableTextTag? recordState, KillParameter killParams, KillCondition killCondition, Action<KillResult>? callBack = null)
    {
        int id = availableId++;
        myRequests.Add(new(id, killer, target, playerState, recordState, killParams, killCondition, callBack));
        RpcRequestKill.Invoke((GamePlayer.LocalPlayer!.PlayerId, id, killer, target, playerState, recordState, PackFlags(killParams, killCondition)));
    }

    private static RemoteProcess<(byte sender, int id, KillResult result)> RpcSendResult = new(
        "KillResult",
        (message, _) =>
        {
            if (message.sender == GamePlayer.LocalPlayer!.PlayerId)
            {
                NebulaGameManager.Instance!.KillRequestHandler.myRequests.RemoveFirst(request => request.Id == message.id)?.CallBack?.Invoke(message.result);
            }
        }
        );

    private static RemoteProcess<(byte sender, int id, GamePlayer killer, GamePlayer target, CommunicableTextTag playerState, CommunicableTextTag? recordState, uint killFlags)> RpcRequestKill = new(
        "RequestKill",
        (message, _) =>
        {
            if (AmongUsClient.Instance.AmHost)
            {
                UnpackFlags(message.killFlags, out var killParam, out var killCond);
                var result = ModFlexibleKill(message.killer.VanillaPlayer, message.target.VanillaPlayer, message.playerState, message.recordState, killParam, killCond);
                RpcSendResult.Invoke((message.sender, message.id, result));
            }
        }
        );

    private static KillResult ModFlexibleKill(PlayerControl killer, PlayerControl target, CommunicableTextTag playerState, CommunicableTextTag? recordState, KillParameter killParam, KillCondition killCond)
    {
        bool CheckKill(PlayerControl? killer, PlayerControl target, CommunicableTextTag playerState, CommunicableTextTag? recordState, bool isMeetingKill, out KillResult result)
        {
            var targetInfo = target.GetModInfo()!;
            var killerInfo = killer?.GetModInfo() ?? targetInfo;

            if(
                (targetInfo.IsDead && killCond.HasFlag(KillCondition.TargetAlive)) ||
                (killerInfo.IsDead && killCond.HasFlag(KillCondition.KillerAlive)))
            {
                result = KillResult.Rejected;
                return false;
            }

            result = GameOperatorManager.Instance?.Run(new PlayerCheckKilledEvent(targetInfo, killerInfo, isMeetingKill, playerState, recordState)).Result ?? KillResult.Kill;

            if (result != KillResult.Kill) RpcOnGuard.Invoke((killerInfo.PlayerId, targetInfo.PlayerId, result == KillResult.ObviousGuard));
            return result == KillResult.Kill;
        }

        bool isMeetingKill = MeetingHud.Instance || ExileController.Instance || !killParam.HasFlag(KillParameter.WithDeadBody);
        if (CheckKill(killer, target, playerState, recordState, isMeetingKill, out var result))
        {
            if (isMeetingKill)
                RpcMeetingKill.Invoke((killer.PlayerId, target.PlayerId, playerState.Id, recordState?.Id ?? int.MaxValue, killParam));
            else
                RpcKill.Invoke((killer.PlayerId, target.PlayerId, playerState.Id, recordState?.Id ?? int.MaxValue, killParam));
        }
        return result;
    }

    static RemoteProcess<(byte killerId, byte targetId, bool targetCanSeeGuard)> RpcOnGuard = new(
    "Guard",
    (message, _) =>
    {
        var killer = NebulaGameManager.Instance?.GetPlayer(message.killerId)!;

        GameOperatorManager.Instance?.Run(new PlayerGuardEvent(NebulaGameManager.Instance?.GetPlayer(message.targetId), killer));

        if (message.killerId == PlayerControl.LocalPlayer.PlayerId || (message.targetCanSeeGuard && message.targetId == PlayerControl.LocalPlayer.PlayerId))
        {
            Helpers.GetPlayer(message.targetId)?.ShowFailedMurder();
        }
    }
    );

    static RemoteProcess<(byte killerId, byte targetId, int stateId, int recordId, KillParameter parameter)> RpcKill = new(
        "Kill",
       (message, _) =>
       {
           var withBlink = message.parameter.HasFlag(KillParameter.WithBlink);

           var recordTag = TranslatableTag.ValueOf(message.recordId);
           if (recordTag != null)
               NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, message.killerId == byte.MaxValue ? null : message.killerId, 1 << message.targetId) { RelatedTag = recordTag });

           var killer = Helpers.GetPlayer(message.killerId == byte.MaxValue ? message.targetId : message.killerId);
           var target = Helpers.GetPlayer(message.targetId);

           if (target == null) return;

           // MurderPlayer ここから

           if (((!target.AmOwner && message.parameter.HasFlag(KillParameter.WithKillSEWidely)) || (killer?.AmOwner ?? false)) && Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(target.KillSfx, false, 0.8f, null);


           target.gameObject.layer = LayerMask.NameToLayer("Ghost");

           if (target.AmOwner)
           {
               //StatsManager.Instance.IncrementStat(StringNames.StatsTimesMurdered);
               if (Minigame.Instance)
               {
                   try
                   {
                       Minigame.Instance.Close();
                       Minigame.Instance.Close();
                   }
                   catch
                   {
                   }
               }
               if (message.parameter.HasFlag(KillParameter.WithOverlay)) DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(killer ? killer!.Data : null, target.Data);
               target.cosmetics.SetNameMask(false);
               target.RpcSetScanner(false);
           }
           if (killer) killer!.MyPhysics.StartCoroutine(killer.KillAnimations[System.Random.Shared.Next(killer.KillAnimations.Count)].CoPerformModKill(killer, target, withBlink).WrapToIl2Cpp());

           // MurderPlayer ここまで


           var targetInfo = target.GetModInfo();

           var killerInfo = killer?.GetModInfo();

           if (targetInfo != null)
           {
               targetInfo.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
               targetInfo.Unbox().MyKiller = killerInfo;
               targetInfo.Unbox().MyState = TranslatableTag.ValueOf(message.stateId);

               var deadState = targetInfo!.PlayerState.TranslationKey;
               if (targetInfo.AmOwner)
               {
                   if(NebulaAchievementManager.GetRecord("death." + deadState, out var rec)) new StaticAchievementToken(rec);
                   new StaticAchievementToken("stats.death." + deadState);
               }
               if (killerInfo?.AmOwner ?? false)
               {
                   if(NebulaAchievementManager.GetRecord("kill." + deadState, out var recKill)) new StaticAchievementToken(recKill);
                   new StaticAchievementToken("stats.kill." + deadState);
               }

               targetInfo.VanillaPlayer.Data.IsDead = true;
               PlayerExtension.ResetOnDying(targetInfo.VanillaPlayer);

               //1ずつ加算するのでこれで十分
               if (targetInfo.AmOwner)
               {
                   if ((NebulaGameManager.Instance?.AllPlayerInfo.Count(p => p.IsDead) ?? 0) == 1) new StaticAchievementToken("firstKillRecord");
                   new StaticAchievementToken("deathRecord");

               }

               //幽霊役職割り当ての前にイベントを発火させる。
               if (killerInfo != null)
               {
                   GameOperatorManager.Instance?.Run(new PlayerKillPlayerEvent(killerInfo, targetInfo), true);
                   GameOperatorManager.Instance?.Run(new PlayerMurderedEvent(targetInfo, killerInfo, withBlink), true);
               }
               else
               {
                   GameOperatorManager.Instance?.Run(new PlayerDieEvent(targetInfo));
               }

               if (message.parameter.HasFlag(KillParameter.WithAssigningGhostRole) && targetInfo.AmOwner) NebulaGameManager.RpcTryAssignGhostRole.Invoke(targetInfo);

           }
       }
       );

    static RemoteProcess<(byte killerId, byte targetId, int stateId, int recordId, KillParameter parameter)> RpcMeetingKill = new(
        "NonPhysicalKill",
       (message, calledByMe) =>
       {
           var recordTag = TranslatableTag.ValueOf(message.recordId);
           if (recordTag != null)
               NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, message.killerId, 1 << message.targetId) { RelatedTag = recordTag });

           var killer = Helpers.GetPlayer(message.killerId);
           var target = Helpers.GetPlayer(message.targetId);

           if (target == null) return;

           if (!target.AmOwner && message.parameter.HasFlag(KillParameter.WithKillSEWidely) && Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(target.KillSfx, false, 0.8f, null);

           target.Die(DeathReason.Exile, false);
           PlayerExtension.ResetOnDying(target);

           if (target.AmOwner)
           {
               if (message.parameter.HasFlag(KillParameter.WithOverlay)) DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(killer ? killer!.Data : null, target.Data);

               NebulaGameManager.Instance!.ChangeToSpectator();
           }


           if (MeetingHud.Instance != null) MeetingHud.Instance.ResetPlayerState();



           var targetInfo = target.GetModInfo();
           var killerInfo = killer.GetModInfo();

           if (targetInfo != null)
           {
               targetInfo.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
               targetInfo.Unbox().MyKiller = killerInfo;
               targetInfo.Unbox().MyState = TranslatableTag.ValueOf(message.stateId);
               if (targetInfo.AmOwner && NebulaAchievementManager.GetRecord("death." + targetInfo!.PlayerState.TranslationKey, out var rec)) new StaticAchievementToken(rec);
               if(targetInfo.AmOwner) new StaticAchievementToken("stats.death." + targetInfo!.PlayerState.TranslationKey);

               if ((killerInfo?.AmOwner ?? false) && NebulaAchievementManager.GetRecord("kill." + targetInfo!.PlayerState.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);


               //Entityイベント発火
               if (killerInfo != null)
               {
                   GameOperatorManager.Instance?.Run(new PlayerKillPlayerEvent(killerInfo, targetInfo), true);
                   GameOperatorManager.Instance?.Run(new PlayerMurderedEvent(targetInfo, killerInfo, false), true);
               }
               else
                   GameOperatorManager.Instance?.Run(new PlayerDieEvent(targetInfo));

               if (message.parameter.HasFlag(KillParameter.WithAssigningGhostRole) && targetInfo.AmOwner) NebulaGameManager.RpcTryAssignGhostRole.Invoke(targetInfo);
           }

           if (MeetingHud.Instance)
           {
               IEnumerator CoGainDiscussionTime()
               {
                   for (int i = 0; i < 10; i++)
                   {
                       MeetingHudExtension.VotingTimer += 1f;
                       MeetingHud.Instance!.lastSecond = 11;
                       yield return new WaitForSeconds(0.1f);
                   }
               }
               NebulaManager.Instance!.StartCoroutine(CoGainDiscussionTime().WrapToIl2Cpp());
           }
       }
       );
}
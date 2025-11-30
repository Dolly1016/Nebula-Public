using MS.Internal.Xml.XPath;
using Nebula.Game.Statistics;
using Nebula.Modules;
using Nebula.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Game;

internal record KillRequestParameter(int Id, GamePlayer Killer, IPlayerlike Target, CommunicableTextTag PlayerState, CommunicableTextTag? RecordState, KillParameter KillParam, KillCondition KillCondition)
{
    static KillRequestParameter()
    {
        new RemoteProcessArgument<KillRequestParameter>((writer, param) =>
        {
            writer.Write(param!.Id);
            RemoteProcessArgument<GamePlayer>.Write(writer, param.Killer);
            RemoteProcessArgument<IPlayerlike>.Write(writer, param.Target);
            RemoteProcessArgument<CommunicableTextTag>.Write(writer, param.PlayerState);
            RemoteProcessArgument<CommunicableTextTag>.Write(writer, param.RecordState);
            writer.Write((int)param.KillParam);
            writer.Write((int)param.KillCondition);
        }, (reader) => new(
            reader.ReadInt32(),
            RemoteProcessArgument<GamePlayer>.Read(reader),
            RemoteProcessArgument<IPlayerlike>.Read(reader),
            RemoteProcessArgument<CommunicableTextTag>.Read(reader),
            RemoteProcessArgument<CommunicableTextTag>.Read(reader),
            (KillParameter)reader.ReadInt32(),
            (KillCondition)reader.ReadInt32()
        ));
    }
}

internal record KillExecutionParameter(byte RequestSender, int RequestId, GamePlayer Killer, IPlayerlike Target, Vector2 TargetPos, GamePlayer RealTarget, KillCharacteristics KillCharacteristics, CommunicableTextTag PlayerState, CommunicableTextTag? RecordState, KillParameter KillParam, bool TargetIsUsingUtility, Vector2 DeadGoalPos)
{
    static KillExecutionParameter()
    {
        new RemoteProcessArgument<KillExecutionParameter>((writer, param) =>
        {
            writer.Write((byte)param!.RequestSender);
            writer.Write((int)param.RequestId);
            RemoteProcessArgument<GamePlayer>.Write(writer, param.Killer);
            RemoteProcessArgument<IPlayerlike>.Write(writer, param.Target);
            RemoteProcessArgument<Vector2>.Write(writer, param.TargetPos);
            RemoteProcessArgument<GamePlayer>.Write(writer, param.RealTarget);
            writer.Write((int)param.KillCharacteristics);
            RemoteProcessArgument<CommunicableTextTag>.Write(writer, param.PlayerState);
            RemoteProcessArgument<CommunicableTextTag>.Write(writer, param.RecordState);
            writer.Write((int)param.KillParam);
            writer.Write(param.TargetIsUsingUtility);
            RemoteProcessArgument<Vector2>.Write(writer, param.DeadGoalPos);
        }, (reader) => new(
            reader.ReadByte(),
            reader.ReadInt32(),
            RemoteProcessArgument<GamePlayer>.Read(reader),
            RemoteProcessArgument<IPlayerlike>.Read(reader),
            RemoteProcessArgument<Vector2>.Read(reader),
            RemoteProcessArgument<GamePlayer>.Read(reader),
            (KillCharacteristics)reader.ReadInt32(),
            RemoteProcessArgument<CommunicableTextTag>.Read(reader),
            RemoteProcessArgument<CommunicableTextTag>.Read(reader),
            (KillParameter)reader.ReadInt32(),
            reader.ReadBoolean(),
            RemoteProcessArgument<Vector2>.Read(reader)
        ));
    }
}

[NebulaRPCHolder]
internal class KillRequestHandler
{
    record KillRequest(int Id, GamePlayer Killer, IPlayerlike Target, CommunicableTextTag PlayerState, CommunicableTextTag? RecordState, KillParameter KillParam, KillCondition KillCondition, Action<KillResult>? CallBack);

    private int availableId = 0;
    private List<KillRequest> myRequests = new();

    public void RequestKill(GamePlayer killer, IPlayerlike target, CommunicableTextTag playerState, CommunicableTextTag? recordState, KillParameter killParams, KillCondition killCondition, Action<KillResult>? callBack = null)
    {
        int id = availableId++;
        myRequests.Add(new(id, killer, target, playerState, recordState, killParams, killCondition, callBack));
        RpcRequestKill.Invoke((GamePlayer.LocalPlayer!.PlayerId, new(id, killer, target, playerState, recordState, killParams, killCondition)));
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

    private static RemoteProcess<(byte sender, KillRequestParameter parameter)> RpcRequestKill = new(
        "RequestKill",
        (message, _) =>
        {
            if (AmongUsClient.Instance.AmHost)
            {
                var result = ModFlexibleKill(message.sender, message.parameter.Id, message.parameter.Killer, message.parameter.Target, message.parameter.PlayerState, message.parameter.RecordState, message.parameter.KillParam, message.parameter.KillCondition);
                RpcSendResult.Invoke((message.sender, message.parameter.Id, result));
            }
        }
        );

    private static KillResult ModFlexibleKill(byte sender, int id, GamePlayer killer, IPlayerlike target, CommunicableTextTag playerState, CommunicableTextTag? recordState, KillParameter killParam, KillCondition killCond)
    {
        bool CheckKill(bool isMeetingKill, out KillResult result)
        {
            result = KillResult.Kill;

            if(killCond.HasFlag(KillCondition.InTaskPhase) && (MeetingHud.Instance || ExileController.Instance))
            {
                result = KillResult.Rejected;
                return false;
            }

            if(target == null)
            {
                result = KillResult.Rejected;
                return false;
            }

            if((target.IsDead && killCond.HasFlag(KillCondition.TargetAlive)) || (killer.IsDead && killCond.HasFlag(KillCondition.KillerAlive)))
            {
                result = KillResult.Rejected;
                return false;
            }

            bool willDieRealTarget = target is GamePlayer || target.KillCharacteristics.HasFlag(KillCharacteristics.FlagKillRealPlayer);
            if (willDieRealTarget)
            {
                var targetRealPlayer = target.RealPlayer!;
                result = GameOperatorManager.Instance?.Run(new PlayerCheckKilledEvent(targetRealPlayer, killer, isMeetingKill, playerState, recordState, killParam)).Result ?? KillResult.Kill;
                if (result != KillResult.Kill) RpcOnGuard.Invoke((killer.PlayerId, targetRealPlayer.PlayerId, result == KillResult.ObviousGuard));
            }
            return result == KillResult.Kill;
        }

        bool inMeetingActually = MeetingHud.Instance || ExileController.Instance;
        bool isMeetingKill = inMeetingActually || !killParam.HasFlag(KillParameter.WithDeadBody);
        if (inMeetingActually)
        {
            bool isAnimating = MeetingHud.Instance && MeetingHud.Instance.state <= MeetingHud.VoteStates.Animating;
            if(!isAnimating) killParam |= KillParameter.WithKillSEWidely;
        }
        if (CheckKill(isMeetingKill, out var result))
        {
            bool usingUtility = target.Logic.InMovingPlat || target.Logic.OnLadder;
            UnityEngine.Vector2 deathPos = target.Position;
            if(target is GamePlayer gamePlayer && gamePlayer.DeathPosition != null)
            {
                if (killParam.HasFlag(KillParameter.WithBlink))
                {
                    deathPos = gamePlayer.DeathPosition.GetNearestPos(killer.Position);
                }
                else 
                {
                    if (gamePlayer.Position.Distance(gamePlayer.DeathPosition.StartPos) < 0.8f)
                        deathPos = gamePlayer.DeathPosition.StartPos;
                    else
                        deathPos = gamePlayer.DeathPosition.GoalPos;
                }
            }
            
            if (isMeetingKill)
                RpcMeetingKill.Invoke(new(sender, id, killer, target, target.Position, target.RealPlayer, target.KillCharacteristics,  playerState, recordState, killParam, usingUtility, deathPos));
            else
                RpcKill.Invoke(new(sender, id, killer, target, target.Position, target.RealPlayer, target.KillCharacteristics, playerState, recordState, killParam, usingUtility, deathPos));
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

    static private bool ShouldHideKillerIfNotBlink => GeneralConfigurations.HideFarKillerOption;
    static RemoteProcess<KillExecutionParameter> RpcKill = new(
        "Kill",
       (KillExecutionParameter param, bool _) =>
       {
           var vanillaKiller = param.Killer?.VanillaPlayer;
           var vanillaRealTarget = param.RealTarget.VanillaPlayer;

           var withBlink = param.KillParam.HasFlag(KillParameter.WithBlink);
           var useViperDeadBody = param.KillParam.HasFlag(KillParameter.WithViperDeadBody);

           bool realTargetWillDie = param.Target is GamePlayer || param.Target.KillCharacteristics.HasFlag(KillCharacteristics.FlagKillRealPlayer);

           if (realTargetWillDie)
           {
               var recordTag = param.RecordState;
               if (recordTag != null)
                   NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, param.Killer == null ? null : param.Killer.PlayerId, 1 << param.RealTarget.PlayerId) { RelatedTag = recordTag });
           }
           

           // MurderPlayer ここから

           //死亡する本人でなくかつ全員にSEを再生する場合、あるいはキルの本人であればキルSEを再生する。
           if (((!realTargetWillDie && param.KillParam.HasFlag(KillParameter.WithKillSEWidely)) || (param.Killer?.AmOwner ?? false)) && Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(vanillaRealTarget.KillSfx, false, 0.8f, null);


           if (realTargetWillDie)
           {
               param.RealTarget.VanillaPlayer.gameObject.layer = LayerMask.NameToLayer("Ghost");
               if (param.RealTarget.AmOwner)
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
                   if (param.KillParam.HasFlag(KillParameter.WithOverlay))
                   {
                       if (param.Killer != null) param.Killer.VanillaPlayer.Data.Role.CustomKillAnimations = useViperDeadBody ? AmongUsUtil.GetRolePrefab<ViperRole>()!.CustomKillAnimations : new(0);

                       if (!withBlink && ShouldHideKillerIfNotBlink && param.Killer != param.Target)
                       {
                           //ShowKillAnimationをさらに改変
                           OverlayKillAnimation[] killAnims = HudManager.Instance.KillOverlay.KillAnims.ToArray();
                           var physAnims = vanillaKiller!.Data.Object.MyPhysics.Animations.GetKillAnimations();
                           if (physAnims.Length > 0) killAnims = physAnims.ToArray();
                           var roleKillAnims = vanillaKiller.Data.Role.CustomKillAnimations;
                           if (roleKillAnims.Length > 0) killAnims = roleKillAnims.ToArray();
                           var anim = killAnims[System.Random.Shared.Next(killAnims.Length)];
                           DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(anim, new KillOverlayInitData(NebulaGameManager.Instance!.UnknownOutfit.outfit, vanillaKiller!.cosmetics.bodyType, param.RealTarget.DefaultOutfit.outfit, PlayerBodyTypes.Normal));
                       }
                       else
                       {
                           DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(vanillaKiller?.Data, param.RealTarget.VanillaPlayer.Data);
                       }
                   }
                   param.RealTarget.VanillaPlayer.cosmetics.SetNameMask(false);
                   param.RealTarget.VanillaPlayer.RpcSetScanner(false);
               }
           }

           vanillaKiller?.MyPhysics.StartCoroutine(vanillaKiller.KillAnimations[System.Random.Shared.Next(vanillaKiller.KillAnimations.Count)].CoPerformModKill(param.RequestSender, param.RequestId, vanillaKiller, new(param.Target, param.TargetPos), param.RealTarget, param.KillCharacteristics, withBlink, param.TargetIsUsingUtility, param.DeadGoalPos, useViperDeadBody, vanillaKiller, param.PlayerState).WrapToIl2Cpp());
           // MurderPlayer ここまで


           if (realTargetWillDie)
           {
               var targetInfo = param.RealTarget?.Unbox();
               var killerInfo = param.Killer?.Unbox();

               if (targetInfo != null)
               {
                   targetInfo.DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
                   targetInfo.MyKiller = killerInfo;

                   var deadState = param.PlayerState;
                   targetInfo.MyState = deadState;

                   if (targetInfo.AmOwner)
                   {
                       if (NebulaAchievementManager.GetRecord("death." + deadState?.TranslationKey, out var rec)) new StaticAchievementToken(rec);
                       new StaticAchievementToken("stats.death." + deadState?.TranslationKey);
                   }
                   if (killerInfo?.AmOwner ?? false)
                   {
                       if (NebulaAchievementManager.GetRecord("kill." + deadState?.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);
                       new StaticAchievementToken("stats.kill." + deadState?.TranslationKey);
                   }

                   targetInfo.MyControl.Data.IsDead = true;
                   PlayerExtension.ResetOnDying(targetInfo.MyControl);

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

                   if (param.KillParam.HasFlag(KillParameter.WithAssigningGhostRole) && targetInfo.AmOwner) NebulaGameManager.RpcTryAssignGhostRole.Invoke(targetInfo);
               }
           }
       }
       );

    static RemoteProcess<KillExecutionParameter> RpcMeetingKill = new(
        "NonPhysicalKill",
      (KillExecutionParameter param, bool _) =>
      {

          var killer = param.Killer;
          var target = param.RealTarget;

          bool realTargetWillDie = param.Target is GamePlayer || param.Target.KillCharacteristics.HasFlag(KillCharacteristics.FlagKillRealPlayer);

          if (realTargetWillDie)
          {
              var recordTag = param.RecordState;
              if (recordTag != null)
                  NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, param.Killer == null ? null : param.Killer.PlayerId, 1 << param.RealTarget.PlayerId) { RelatedTag = recordTag });
          }

          //死亡する本人でなくかつ全員にSEを再生する場合、あるいはキルの本人であればキルSEを再生する。
          bool amKiller = param.Killer?.AmOwner ?? false;
          bool amTarget = param.Target.AmOwner;
          bool everyoneShouldHearSE = realTargetWillDie && param.KillParam.HasFlag(KillParameter.WithKillSEWidely);
          if ((amKiller || (everyoneShouldHearSE && !amTarget)) && Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(param.RealTarget.VanillaPlayer.KillSfx, false, 0.8f, null);

          if (param.Target is IFakePlayer fp)
          {
              GameOperatorManager.Instance?.Run(new PlayerKillFakePlayerEvent(fp, param.Killer!));
              fp.Release();
          }

          if (realTargetWillDie)
          {
              target.VanillaPlayer.Die(DeathReason.Exile, false);
              PlayerExtension.ResetOnDying(target.VanillaPlayer);

              if (target.AmOwner)
              {
                  if (param.KillParam.HasFlag(KillParameter.WithOverlay))
                  {
                      DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(killer?.VanillaPlayer.Data, target.VanillaPlayer.Data);
                  }
                  NebulaGameManager.Instance!.ChangeToSpectator();
              }


              if (MeetingHud.Instance != null) MeetingHud.Instance.ResetPlayerState();



              target.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
              target.Unbox().MyKiller = killer;
              target.Unbox().MyState = param.PlayerState;
              if (target.AmOwner && NebulaAchievementManager.GetRecord("death." + target!.PlayerState.TranslationKey, out var rec)) new StaticAchievementToken(rec);
              if (target.AmOwner) new StaticAchievementToken("stats.death." + target!.PlayerState.TranslationKey);

              if ((killer?.AmOwner ?? false) && NebulaAchievementManager.GetRecord("kill." + target!.PlayerState.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);


              //Entityイベント発火
              if (killer != null)
              {
                  GameOperatorManager.Instance?.Run(new PlayerKillPlayerEvent(killer, target), true);
                  GameOperatorManager.Instance?.Run(new PlayerMurderedEvent(target, killer, false), true);
              }
              else
                  GameOperatorManager.Instance?.Run(new PlayerDieEvent(target));

              if (param.KillParam.HasFlag(KillParameter.WithAssigningGhostRole) && target.AmOwner) NebulaGameManager.RpcTryAssignGhostRole.Invoke(target);


              if (MeetingHud.Instance) MeetingHudExtension.ExpandDiscussionTime();
          }
      }
       );
}
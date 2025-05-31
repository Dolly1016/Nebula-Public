using Nebula.Behavior;
using Nebula.Roles.Impostor;
using Nebula.Roles.Modifier;
using Nebula.Roles.Neutral;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Game;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class NebulaEndCriteria
{
    static NebulaEndCriteria()
    {
        DIManager.Instance.RegisterGeneralModule<IGameModeStandard>(() => new SabotageCriteria().Register(NebulaAPI.CurrentGame!));
        DIManager.Instance.RegisterGeneralModule<IGameModeStandard>(() => new CrewmateCriteria().Register(NebulaAPI.CurrentGame!));
        DIManager.Instance.RegisterGeneralModule<IGameModeStandard>(() => new ImpostorCriteria().Register(NebulaAPI.CurrentGame!));
        DIManager.Instance.RegisterGeneralModule<IGameModeStandard>(() => new JackalCriteria().Register(NebulaAPI.CurrentGame!));
        DIManager.Instance.RegisterGeneralModule<IGameModeStandard>(() => new LoversCriteria().Register(NebulaAPI.CurrentGame!));
        DIManager.Instance.RegisterGeneralModule<IGameModeStandard>(() => new JesterCriteria().Register(NebulaAPI.CurrentGame!));

    }


    int gameModeMask;

    public bool IsValidCriteria => (gameModeMask & GeneralConfigurations.CurrentGameMode) != 0;

    public Func<GameEnd?>? OnUpdate = null;
    public Func<PlayerControl?, Tuple<GameEnd, int>?>? OnExiled = null;
    public Func<GameEnd?>? OnTaskUpdated = null;

    public NebulaEndCriteria(int gameModeMask = 0xFFFF)
    {
        this.gameModeMask = gameModeMask;
    }

    private class SabotageCriteria : IModule, IGameOperator
    {
        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (ShipStatus.Instance != null)
            {
                var status = ShipStatus.Instance;
                if (status.Systems != null)
                {
                    ISystemType? systemType = status.Systems.ContainsKey(SystemTypes.LifeSupp) ? status.Systems[SystemTypes.LifeSupp] : null;
                    if (systemType != null)
                    {
                        LifeSuppSystemType lifeSuppSystemType = systemType.TryCast<LifeSuppSystemType>()!;
                        if (lifeSuppSystemType != null && lifeSuppSystemType.Countdown < 0f)
                        {
                            lifeSuppSystemType.Countdown = 10000f;
                            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Sabotage);
                        }
                    }

                    foreach (ISystemType systemType2 in ShipStatus.Instance.Systems.Values)
                    {
                        ICriticalSabotage? criticalSabotage = systemType2.TryCast<ICriticalSabotage>();
                        if (criticalSabotage != null && criticalSabotage.Countdown < 0f)
                        {
                            criticalSabotage.ClearSabotage();
                            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Sabotage);
                        }
                    }
                }
            }
        }
    };

    private class CrewmateCriteria : IModule, IGameOperator
    {
        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (NebulaGameManager.Instance?.AllPlayerInfo.Any(p =>
            {
                if (p.IsDead) return false;
                if (p.Role.Role.Team == Impostor.MyTeam) return true;
                if (p.Role.Role.Team == Jackal.MyTeam || p.Modifiers.Any(m => m.Modifier == SidekickModifier.MyRole)) return true;
                return false;
            }) ?? true) return;

            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.CrewmateWin, GameEndReason.Situation);
        }

        [OnlyHost]
        void OnTaskUpdate(PlayerTaskUpdateEvent ev)
        {
            int quota = 0;
            int completed = 0;
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDisconnected) continue;

                if (!p.Tasks.IsCrewmateTask) continue;
                quota += p.Tasks.Quota;
                completed += p.Tasks.TotalCompleted;
            }
            if (quota > 0 && quota <= completed) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.CrewmateWin, GameEndReason.Task);
        }
    };

    private class ImpostorCriteria : IModule, IGameOperator
    {
        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            int impostors = 0;
            int totalAlive = 0;
            
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDead) continue;
                totalAlive++;

                //相方生存Loversではないインポスターのみカウントに入れる
                if (p.Role.Role.Team == Impostor.MyTeam && (!p.Unbox().TryGetModifier<Lover.Instance>(out var lover) || lover.IsAloneLover)) impostors++;

                //ジャッカル陣営が生存している間は勝利できない
                if (p.Role.Role.Team == Jackal.MyTeam || p.Unbox().AllModifiers.Any(m => m.Modifier == SidekickModifier.MyRole)) return;
            }

            if(impostors * 2 >= totalAlive) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Situation);
        }
    };

    private class JackalCriteria : IModule, IGameOperator
    {
        List<Jackal.Instance> allAliveJackals = [];
        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            int totalAlive = 0;
            bool leftImpostors = false;
            static bool isJackalTeam(GamePlayer p) => p.Role.Role.Team == Jackal.MyTeam || p.Unbox().AllModifiers.Any(m => m.Modifier == SidekickModifier.MyRole);

            //全生存者数を数えつつ、インポスターが生存していたらチェックをやめる
            allAliveJackals.Clear();
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDead) continue;

                //インポスターが生存している間は勝利できない
                if (p.Role.Role.Team == Impostor.MyTeam) leftImpostors = true;
                if (p.Role is Jackal.Instance jRole) allAliveJackals.Add(jRole);
                totalAlive++;
            }

            

            ulong jackalMask = 0;
            int teamCount = 0;
            int winningJackalTeams = 0;
            ulong completeWinningJackalMask = 0;

            //全ジャッカルに対して、各チームごとに勝敗を調べる
            foreach (var jackal in allAliveJackals)
            {
                //ジャッカル陣営の数をカウントする
                ulong myMask = 1ul << jackal!.JackalTeamId;
                if ((jackalMask & myMask) == 0) teamCount++;
                else continue; //既に考慮したチームはスキップしてよい
                jackalMask |= myMask;

                //死亡しておらず、同チーム、かつラバーズでないか相方死亡ラバー
                int aliveJackals = NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead && jackal!.IsSameTeam(p) && (!p.TryGetModifier<Lover.Instance>(out var lover) || lover.IsAloneLover) && !p.IsMadmate);


                //完全殲滅勝利
                if (aliveJackals == totalAlive) completeWinningJackalMask |= myMask;
                //キル勝利
                if (aliveJackals * 2 >= totalAlive && !leftImpostors) winningJackalTeams++;
            }

            //キル勝利のトリガー
            if(teamCount == 1 && winningJackalTeams > 0) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JackalWin, GameEndReason.Situation);
            //完全殲滅勝利のトリガー
            if(completeWinningJackalMask != 0)
            {
                allAliveJackals.Do(j => j.IsDefeatedJackal = (completeWinningJackalMask & (1ul << j.JackalTeamId)) == 0ul);
                NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JackalWin, GameEndReason.Situation);
            }
        }
    };

    private class LoversCriteria : IModule, IGameOperator
    {
        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            int totalAlive = NebulaGameManager.Instance!.AllPlayerInfo.Count((p) => !p.IsDead);
            if (totalAlive > 3) return;

            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDead) continue;
                if (p.Unbox().TryGetModifier<Lover.Instance>(out var lover)){
                    if (lover.MyLover.Get()?.IsDead ?? true) continue;

                    NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.LoversWin, GameEndReason.Situation);
                }

            }

            return;
        }
    };

    private class JesterCriteria : IModule, IGameOperator
    {
        [OnlyHost]
        void OnExiled(PlayerExiledEvent ev) 
        {
            if (ev.Player?.Role.Role == Roles.Neutral.Jester.MyRole && (!Jester.RequiresTasksForWin || (ev.Player?.Tasks.IsCompletedCurrentTasks ?? false))) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JesterWin, GameEndReason.Special, BitMasks.AsPlayer(1u << ev.Player.PlayerId));
        }
    };
}

public class CriteriaManager
{
    private record TriggeredGameEnd(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason reason, EditableBitMask<Virial.Game.Player>? additionalWinners);
    private List<TriggeredGameEnd> triggeredGameEnds = [];
    
    public void Trigger(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason reason, EditableBitMask<Virial.Game.Player>? additionalWinners)
    {
        if (additionalWinners != null && reason == GameEndReason.Special && triggeredGameEnds.Find(end => end.gameEnd == gameEnd && end.reason == reason && end.additionalWinners != null, out var end))
        {
            GamePlayer.AllPlayers.Where(additionalWinners.Test).Do(p => end.additionalWinners!.Add(p));
        }
        else
        {
            triggeredGameEnds.Add(new(gameEnd, reason, additionalWinners));
        }
    }

    public void CheckAndTriggerGameEnd()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        //終了条件が確定済みなら何もしない
        if (NebulaGameManager.Instance?.EndState != null) return;

        if ((ExileController.Instance) && !Minigame.Instance)
        {
            triggeredGameEnds.RemoveAll(t => t.reason is GameEndReason.Situation or GameEndReason.SpecialSituation);
            return; //追放中はゲーム終了条件の判定をスキップする。
        }

        if(triggeredGameEnds.Count == 0) return;

        var end = triggeredGameEnds.MaxBy(g => g.gameEnd.Priority);
        triggeredGameEnds.Clear();

        if (end == null) return;

        NebulaGameManager.Instance?.InvokeEndGame(end.gameEnd, end.reason, end.additionalWinners != null ? (NebulaGameManager.Instance.AllPlayerInfo.Aggregate(0, (v, p) => end.additionalWinners.Test(p) ? (v | (1 << p.PlayerId)) : v)) : 0);
    }
}

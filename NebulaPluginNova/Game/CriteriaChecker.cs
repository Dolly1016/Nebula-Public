using Nebula.Behaviour;
using Nebula.Roles.Impostor;
using Nebula.Roles.Modifier;
using Nebula.Roles.Neutral;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

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

    public Func<CustomEndCondition?>? OnUpdate = null;
    public Func<PlayerControl?, Tuple<CustomEndCondition, int>?>? OnExiled = null;
    public Func<CustomEndCondition?>? OnTaskUpdated = null;

    public NebulaEndCriteria(int gameModeMask = 0xFFFF)
    {
        this.gameModeMask = gameModeMask;
    }

    private class SabotageCriteria : IModule, IGameOperator
    {
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
        void OnUpdate(GameUpdateEvent ev)
        {
            if (NebulaGameManager.Instance?.AllPlayerInfo().Any(p =>
            {
                if (p.IsDead) return false;
                if (p.Role.Role.Team == Impostor.MyTeam) return true;
                if (p.Role.Role.Team == Jackal.MyTeam || p.Modifiers.Any(m => m.Modifier == SidekickModifier.MyRole)) return true;
                return false;
            }) ?? true) return;

            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.CrewmateWin, GameEndReason.Situation);
        }

        void OnTaskUpdate(PlayerTaskUpdateEvent ev)
        {
            int quota = 0;
            int completed = 0;
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
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
        void OnUpdate(GameUpdateEvent ev)
        {
            int impostors = 0;
            int totalAlive = 0;
            
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (p.IsDead) continue;
                totalAlive++;

                //Loversではないインポスターのみカウントに入れる
                if (p.Role.Role.Team == Impostor.MyTeam && !p.Unbox().TryGetModifier<Lover.Instance>(out _)) impostors++;

                //ジャッカル陣営が生存している間は勝利できない
                if (p.Role.Role.Team == Jackal.MyTeam || p.Unbox().AllModifiers.Any(m => m.Modifier == SidekickModifier.MyRole)) return;
            }

            if(impostors * 2 >= totalAlive) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Situation);
        }
    };

    private class JackalCriteria : IModule, IGameOperator
    {
        void OnUpdate(GameUpdateEvent ev)
        {
            int totalAlive = NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead);

            bool isJackalTeam(GamePlayer p) => p.Role.Role.Team == Jackal.MyTeam || p.Unbox().AllModifiers.Any(m => m.Modifier == SidekickModifier.MyRole);

            int totalAliveAllJackals = 0;

            //全体の生存しているジャッカルの人数を数えると同時に、ジャッカル陣営が勝利できない状況なら調べるのをやめる
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (p.IsDead) continue;

                if (isJackalTeam(p)) totalAliveAllJackals++;

                //ラバーズが生存している間は勝利できない
                if (p.Unbox().TryGetModifier<Lover.Instance>(out _)) return;
                //インポスターが生存している間は勝利できない
                if (p.Role.Role.Team == Impostor.MyTeam) return;
            }

            //全ジャッカルに対して、各チームごとに勝敗を調べる
            foreach (var jackal in NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && p.Role.Role == Roles.Neutral.Jackal.MyRole))
            {
                var jRole = (jackal.Role as Roles.Neutral.Jackal.Instance);
                if (!(jRole?.CanWinDueToKilling ?? false)) continue;

                int aliveJackals = NebulaGameManager.Instance!.AllPlayerInfo().Count(p => !p.IsDead && (jRole!.IsSameTeam(p)));
                
                //他のJackal陣営が生きていたら勝利できない
                if (aliveJackals < totalAliveAllJackals) continue;

                if (aliveJackals * 2 >= totalAlive) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JackalWin, GameEndReason.Situation);
            }
        }
    };

    private class LoversCriteria : IModule, IGameOperator
    {
        void OnUpdate(GameUpdateEvent ev)
        {
            int totalAlive = NebulaGameManager.Instance!.AllPlayerInfo().Count((p) => !p.IsDead);
            if (totalAlive != 3) return;

            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
            {
                if (p.IsDead) continue;
                totalAlive++;
                if (p.Unbox().TryGetModifier<Lover.Instance>(out var lover)){
                    if (lover.MyLover?.IsDead ?? true) continue;

                    NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.LoversWin, GameEndReason.Situation);
                }

            }

            return;
        }
    };

    private class JesterCriteria : IModule, IGameOperator
    {
        void OnExiled(PlayerExiledEvent ev) 
        {
            if (ev.Player?.Role.Role == Roles.Neutral.Jester.MyRole) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.JesterWin, GameEndReason.Special, BitMasks.AsPlayer(1u << ev.Player.PlayerId));
        }
    };
}

public class CriteriaManager
{
    private record TriggeredGameEnd(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason reason, BitMask<Virial.Game.Player>? additionalWinners);
    private List<TriggeredGameEnd> triggeredGameEnds = new();
    
    public void Trigger(Virial.Game.GameEnd gameEnd, Virial.Game.GameEndReason reason, BitMask<Virial.Game.Player>? additionalWinners)
    {
        triggeredGameEnds.Add(new(gameEnd, reason, additionalWinners));
    }

    public void CheckAndTriggerGameEnd()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        //終了条件が確定済みなら何もしない
        if (NebulaGameManager.Instance?.EndState != null) return;

        if ((ExileController.Instance) && !Minigame.Instance) triggeredGameEnds.RemoveAll(t => t.reason == GameEndReason.Situation);

        if(triggeredGameEnds.Count == 0) return;

        var end = triggeredGameEnds.MaxBy(g => g.gameEnd.Priority);
        triggeredGameEnds.Clear();

        if (end == null) return;

        NebulaGameManager.Instance?.InvokeEndGame(end.gameEnd, end.reason, end.additionalWinners != null ? (NebulaGameManager.Instance.AllPlayerInfo().Aggregate(0, (v, p) => end.additionalWinners.Test(p) ? (v | (1 << p.PlayerId)) : v)) : 0);
    }
}

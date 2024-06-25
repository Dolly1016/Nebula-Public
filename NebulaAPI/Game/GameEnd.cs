namespace Virial.Game;

public interface GameEnd
{
    internal int Priority { get; }
    internal byte Id { get; }
}

public interface ExtraWin
{
    internal byte Id { get; }
    internal ulong ExtraWinMask { get; }
}

public enum GameEndReason
{
    Task,
    Situation,
    Special,
    Sabotage
}

public enum ExtraWinCheckPhase
{
    Phase0,
    Phase1,
    Phase2,
    Phase3,
    PhaseMax,

    LoversPhase = Phase2,
    ObsessionPhase = Phase1,
    DancerPhase = Phase0,
}

public static class NebulaGameEnds
{
    public static GameEnd CrewmateGameEnd { get; internal set; } = null!;
    public static GameEnd ImpostorGameEnd { get; internal set; } = null!;
    public static GameEnd ArsonistGameEnd { get; internal set; } = null!;
    public static GameEnd JackalGameEnd { get; internal set; } = null!;
    public static GameEnd JesterGameEnd { get; internal set; } = null!;
    public static GameEnd PaparazzoGameEnd { get; internal set; } = null!;
    public static GameEnd VultureGameEnd { get; internal set; } = null!;
}

public class EndState
{

    public BitMask<Virial.Game.Player> Winners { get; private init; }
    public BitMask<ExtraWin> ExtraWins { get; private init; }
    public GameEndReason EndReason { get; private init; }
    public GameEnd EndCondition { get; private init; }

    public EndState(BitMask<Virial.Game.Player> winners, GameEnd endCondition, GameEndReason reason, BitMask<ExtraWin> extraWins)
    {
        this.Winners = winners;
        this.EndCondition = endCondition;
        this.ExtraWins = extraWins;
        EndReason = reason;
    }
}
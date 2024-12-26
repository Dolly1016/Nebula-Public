namespace Virial.Game;

public interface GameEnd
{
    internal int Priority { get; }
    internal byte Id { get; }
    bool AllowWin { get; }
}

public interface ExtraWin
{
    internal byte Id { get; }
    internal ulong ExtraWinMask { get; }
}

public enum GameEndReason
{
    /// <summary>
    /// タスク完遂によるゲーム終了です。
    /// </summary>
    Task,
    /// <summary>
    /// 人数都合によるゲーム終了です。
    /// ゲーム終了が無視されるケースがあるため、終了条件を満たしているならば常時ゲーム終了を要求する必要があります。
    /// </summary>
    Situation,
    /// <summary>
    /// 人数都合と同様、追放中はゲーム終了を要求しないシチュエーション都合のゲーム終了です。
    /// ゲーム終了が無視されるケースがあるため、終了条件を満たしているならば常時ゲーム終了を要求する必要があります。
    /// </summary>
    SpecialSituation,
    /// <summary>
    /// 特殊な条件によるゲーム終了です。
    /// </summary>
    Special,
    /// <summary>
    /// サボタージュによるゲーム終了です。
    /// </summary>
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
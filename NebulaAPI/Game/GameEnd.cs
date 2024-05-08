namespace Virial.Game;

public interface GameEnd
{
    internal int Priority { get; }
}

public enum GameEndReason
{
    Task,
    Situation,
    Special,
    Sabotage
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

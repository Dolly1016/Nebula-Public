using System.Diagnostics.CodeAnalysis;
using Virial.Text;
using Virial.Utilities;

namespace Virial.Game;

/// <summary>
/// ゲーム終了を表します。
/// <see cref="Virial.Runtime.NebulaPreprocessor.CreateTeam(string, Virial.Color, Virial.Assignable.TeamRevealType)"/>
/// </summary>
public sealed class GameEnd
{
    static private readonly Dictionary<byte, GameEnd> allEndConditions = [];
    static internal IEnumerable<GameEnd> AllEndConditions => allEndConditions.Values;
    static public bool TryGet(byte id, [MaybeNullWhen(false)] out GameEnd gameEnd) => allEndConditions.TryGetValue(id, out gameEnd);
    static private byte usedIdForAddon = 63;
    public int Priority { get; }
    internal byte Id { get; }
    public bool AllowWin { get; internal init; }
    public TextComponent DisplayText { get; private init; }
    internal UnityEngine.Color Color { get; }
    internal string ImmutableId { get; private init; }


    internal GameEnd(byte id, string immutableId, TextComponent dislayText, UnityEngine.Color color, int priority, bool allowWin = true)
    {
        Priority = priority;
        Id = id;
        AllowWin = allowWin;
        DisplayText = dislayText;
        Color = color;

        allEndConditions.Add(id, this);
    }

    internal GameEnd(byte id, string localizedName, UnityEngine.Color color, int priority, bool allowWin = true) : this(id, localizedName, NebulaAPI.GUI.LocalizedTextComponent("end." + localizedName), color, priority, allowWin) { }

    internal GameEnd(string immutableId, TextComponent dislayText, Virial.Color color, int priority) : this(++usedIdForAddon, immutableId, dislayText, color.ToUnityColor(), priority) { }
    internal GameEnd(string localizedName, Virial.Color color, int priority) : this(++usedIdForAddon, localizedName, color.ToUnityColor(), priority) { }
}

public sealed class ExtraWin
{
    static private readonly Dictionary<byte, ExtraWin> allExtraWin = [];
    static internal IEnumerable<ExtraWin> AllExtraWins => allExtraWin.Values;
    static public bool TryGet(byte id, [MaybeNullWhen(false)] out ExtraWin extraWin) => allExtraWin.TryGetValue(id, out extraWin);

    internal byte Id { get; private init; }
    internal ulong ExtraWinMask => 1ul << Id;
    static private byte usedIdForAddon = 16;

    internal string ImmutableId { get; private init; }
    public TextComponent DisplayText { get; private init; }
    internal UnityEngine.Color Color { get; }

    internal ExtraWin(byte id, string immutableId, TextComponent displayText, Virial.Color color){
        Id = id;
        ImmutableId = immutableId;
        DisplayText = displayText;

        allExtraWin.Add(id, this);
    }
    internal ExtraWin(byte id, string localizedName, Virial.Color color) : this(id, localizedName, NebulaAPI.GUI.LocalizedTextComponent("end.extra." + localizedName).Color(color), color) { }
    internal ExtraWin(string localizedName, Virial.Color color) : this(++usedIdForAddon, localizedName, color) { }
    internal ExtraWin(string immutableId, TextComponent displayText, Virial.Color color) : this(++usedIdForAddon, immutableId, displayText, color) { }
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
    GrudgePhase = Phase0,
    TrilemmaPhase = Phase2,
    ScarletPhase = Phase1,
    MadmatePhase = Phase0,
}

public static class NebulaGameEnds
{
    /// <summary>
    /// クルーメイト勝利。
    /// </summary>
    public readonly static Cache<GameEnd> CrewmateGameEnd = new(() => GameEnd.TryGet(16, out var end) ? end : null!);
    /// <summary>
    /// インポスター勝利。
    /// </summary>
    public readonly static Cache<GameEnd> ImpostorGameEnd = new(() => GameEnd.TryGet(17, out var end) ? end : null!);
    /// <summary>
    /// アーソニスト勝利。
    /// </summary>
    public readonly static Cache<GameEnd> ArsonistGameEnd = new(() => GameEnd.TryGet(27, out var end) ? end : null!);
    /// <summary>
    /// アヴェンジャー勝利。
    /// </summary>
    public readonly static Cache<GameEnd> AvengerGameEnd = new(() => GameEnd.TryGet(30, out var end) ? end : null!);
    /// <summary>
    /// ダンサー勝利。
    /// </summary>
    public readonly static Cache<GameEnd> DancerGameEnd = new(() => GameEnd.TryGet(31, out var end) ? end : null!);
    /// <summary>
    /// ギャンブラー勝利。
    /// </summary>
    public readonly static Cache<GameEnd> GamblerGameEnd = new(() => GameEnd.TryGet(35, out var end) ? end : null!);
    /// <summary>
    /// ジャッカル勝利。
    /// </summary>
    public readonly static Cache<GameEnd> JackalGameEnd = new(() => GameEnd.TryGet(26, out var end) ? end : null!);
    /// <summary>
    /// ジェスター勝利。
    /// </summary>
    public readonly static Cache<GameEnd> JesterGameEnd = new(() => GameEnd.TryGet(25, out var end) ? end : null!);
    /// <summary>
    /// ラバーズ勝利。
    /// </summary>
    public readonly static Cache<GameEnd> LoversGameEnd = new(() => GameEnd.TryGet(28, out var end) ? end : null!);
    /// <summary>
    /// パパラッチ勝利。
    /// </summary>
    public readonly static Cache<GameEnd> PaparazzoGameEnd = new(() => GameEnd.TryGet(29, out var end) ? end : null!);
    /// <summary>
    /// スカーレット勝利。
    /// </summary>
    public readonly static Cache<GameEnd> ScarletGameEnd = new(() => GameEnd.TryGet(32, out var end) ? end : null!);
    /// <summary>
    /// 妖狐勝利。
    /// </summary>
    public readonly static Cache<GameEnd> SpectreGameEnd = new(() => GameEnd.TryGet(33, out var end) ? end : null!);
    /// <summary>
    /// トリレンマ単独勝利。
    /// </summary>
    public readonly static Cache<GameEnd> TrilemmaGameEnd = new(() => GameEnd.TryGet(34, out var end) ? end : null!);
    /// <summary>
    /// ヴァルチャー勝利。
    /// </summary>
    public readonly static Cache<GameEnd> VultureGameEnd = new(() => GameEnd.TryGet(24, out var end) ? end : null!);
    /// <summary>
    /// 無効なゲーム。
    /// </summary>
    public readonly static Cache<GameEnd> NoGameEnd = new(() => GameEnd.TryGet(63, out var end) ? end : null!);
}

public class EndState
{

    public BitMask<Virial.Game.Player> Winners { get; private init; }
    public BitMask<ExtraWin> ExtraWins { get; private init; }
    public GameEndReason EndReason { get; private init; }
    public GameEnd EndCondition { get; private init; }
    public GameEndReason OriginalEndReason { get; private init; }
    public GameEnd OriginalEndCondition { get; private init; }

    public EndState(BitMask<Virial.Game.Player> winners, GameEnd endCondition, GameEndReason reason, GameEnd originalEndCondition, GameEndReason originalReason, BitMask<ExtraWin> extraWins)
    {
        this.Winners = winners;
        this.EndCondition = endCondition;
        this.ExtraWins = extraWins;
        EndReason = reason;
        this.OriginalEndCondition = originalEndCondition;
        this.OriginalEndReason = originalReason;
    }
}
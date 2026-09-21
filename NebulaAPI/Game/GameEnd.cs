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
    internal Virial.Color Color { get; }
    internal string ImmutableId { get; private init; }
    internal Func<UnityEngine.AudioClip?>? AlternativeClip { get; init; }
    public bool SpecifyNobodyWins { get; private init; }


    internal GameEnd(byte id, string immutableId, TextComponent dislayText, Virial.Color color, int priority, bool allowWin = true, bool specifyNobodyWins = true)
    {
        Priority = priority;
        Id = id;
        this.ImmutableId = immutableId;
        AllowWin = allowWin;
        DisplayText = dislayText;
        Color = color;
        this.SpecifyNobodyWins = specifyNobodyWins;
        allEndConditions.Add(id, this);
    }

    internal GameEnd(byte id, string localizedName, Virial.Color color, int priority, bool allowWin = true, bool specifyNobodyWins = true) : this(id, localizedName, NebulaAPI.GUI.LocalizedTextComponent("end." + localizedName), color, priority, allowWin, specifyNobodyWins) { }

    internal GameEnd(string immutableId, TextComponent dislayText, Virial.Color color, int priority, bool specifyNobodyWins = true) : this(++usedIdForAddon, immutableId, dislayText, color, priority, specifyNobodyWins: specifyNobodyWins) { }
    internal GameEnd(string localizedName, Virial.Color color, int priority, bool specifyNobodyWins = true) : this(++usedIdForAddon, localizedName, color, priority, specifyNobodyWins: specifyNobodyWins) { }
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

public static class GameEndReasonHelper
{
    static public bool IsSpecial(this GameEndReason reason) => reason is GameEndReason.Special or GameEndReason.SpecialSituation;
}

public enum ExtraWinCheckPhase
{
    Phase0,
    Phase1,
    Phase2,
    Phase3,
    Phase4,
    Phase5,
    Phase6,
    Phase7,
    PhaseMax,

    LoversPhase = Phase2,
    ObsessionPhase = Phase1,
    DancerPhase = Phase0,
    GrudgePhase = Phase0,
    OpportunistPhase = Phase0,
    TrilemmaPhase = Phase2,
    ScarletPhase = Phase0,
    MadmatePhase = Phase0,
    VanityPhase = Phase0,
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

public class GameEndStage
{
    public GameEnd EndCondition { get; private init; }

    public GameEndReason EndReason { get; private init; }

    public BitMask<Virial.Game.Player> Winners { get; private init; }

    public BitMask<ExtraWin> ExtraWins { get; private init; }

    public GameEndDetail? Detail { get; private init; }

    public GameEndStage(GameEnd endCondition, GameEndReason endReason, BitMask<Virial.Game.Player> winners, BitMask<ExtraWin> extraWins, GameEndDetail? detail)
    {
        EndCondition = endCondition;
        EndReason = endReason;
        Winners = winners;
        ExtraWins = extraWins;
        Detail = detail;
    }
}

public class EndState
{
    private readonly GameEndStage[] stages;

    public IReadOnlyList<GameEndStage> Stages => stages;

    public GameEndStage OriginalStage => stages[0];

    public GameEndStage FinalStage => stages[^1];

    public BitMask<Virial.Game.Player> Winners { get; private init; }
    public BitMask<ExtraWin> ExtraWins { get; private init; }
    public GameEndReason EndReason { get; private init; }
    public GameEnd EndCondition { get; private init; }
    public GameEndReason OriginalEndReason { get; private init; }
    public GameEnd OriginalEndCondition { get; private init; }

    public EndState(params GameEndStage[] stages)
    {
        if (stages.Length == 0) throw new ArgumentException("EndState requires one or more stages.", nameof(stages));

        this.stages = stages;

        var original = stages[0];
        var final = stages[^1];

        Winners = final.Winners;
        ExtraWins = final.ExtraWins;
        EndReason = final.EndReason;
        EndCondition = final.EndCondition;
        OriginalEndReason = original.EndReason;
        OriginalEndCondition = original.EndCondition;
    }
}

internal class GameEndPhase
{
    public CommunicableTextTag Name { get; private init; }
    internal BitMask<Virial.Game.Player> Winners { get; private init; }
    internal IReadOnlyList<(CommunicableTextTag reason, BitMask<Virial.Game.Player> players)> Reasons { get; private init; }

    internal GameEndPhase(CommunicableTextTag name, BitMask<Virial.Game.Player> winners, IReadOnlyList<(CommunicableTextTag, BitMask<Virial.Game.Player>)> reasons)
    {
        Name = name;
        Winners = winners;
        Reasons = reasons;
    }
}

/// <summary>
/// 勝敗がどう決まったのかの記録。
/// </summary>
public class GameEndDetail
{
    private readonly List<GameEndPhase> phases = [];
    private readonly List<(CommunicableTextTag reason, uint players)> pendingReasons = [];
    private uint lastWinners = 0u;

    internal IReadOnlyList<GameEndPhase> Phases => phases;

    /// <summary>
    /// プレイヤーの勝敗が変化する、あるいは変化しない理由を追加します。
    /// </summary>
    /// <param name="playerId">理由を与えるプレイヤーのID。</param>
    /// <param name="reason">理由。</param>
    public void AddReason(byte playerId, CommunicableTextTag reason)
    {
        if (reason == null) return;

        var bit = 1u << playerId;
        var index = pendingReasons.FindIndex(entry => entry.reason == reason);

        if (index < 0) pendingReasons.Add((reason, bit));
        else pendingReasons[index] = (reason, pendingReasons[index].players | bit);
    }

    internal void EndPhase(CommunicableTextTag name, BitMask<Virial.Game.Player> winners)
    {
        var raw = winners?.AsRawPattern ?? 0u;

        // 冗長なフェーズの削除
        if (pendingReasons.Count == 0 && raw == lastWinners)
        {
            pendingReasons.Clear();
            return;
        }

        phases.Add(new GameEndPhase(name, winners ?? BitMasks.AsPlayer(raw), pendingReasons.Select(entry => (entry.reason, (BitMask<Virial.Game.Player>)BitMasks.AsPlayer(entry.players))).ToArray()));

        lastWinners = raw;
        pendingReasons.Clear();
    }
}
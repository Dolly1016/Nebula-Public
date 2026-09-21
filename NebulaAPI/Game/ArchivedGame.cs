using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;
using Virial.Text;

namespace Virial.Game;

public interface IArchivedPlayer
{
    Virial.Game.OutfitDefinition DefaultOutfit { get; }
    string PlayerName => DefaultOutfit.outfit.PlayerName;
    byte PlayerId { get; }
}

public record ArchivedColor(Virial.Color MainColor, Virial.Color ShadowColor, Virial.Color VisorColor);

/// <summary>
/// 見た目の色を、後から読み返せる形で表します。
/// </summary>
/// <remarks>
/// 配色は設定で変えられるため、色IDではなく実際の値を <c>#RRGGBB</c> の文字列で持ちます。
/// </remarks>
/// <param name="Main">体の色。</param>
/// <param name="Shadow">影の色。</param>
/// <param name="Visor">バイザーの色。</param>
public record ArchivedPlayerColor(string Main, string Shadow, string Visor)
{
    public string Main { get; init; } = Main ?? "#FFFFFF";
    public string Shadow { get; init; } = Shadow ?? "#FFFFFF";
    public string Visor { get; init; } = Visor ?? "#FFFFFF";

    public static ArchivedPlayerColor From(ArchivedColor color) =>
        new(ToHex(color.MainColor), ToHex(color.ShadowColor), ToHex(color.VisorColor));

    private static string ToHex(Virial.Color color)
    {
        static int Channel(float v) => Math.Clamp((int)Math.Round(v * 255f), 0, 255);
        return $"#{Channel(color.R):X2}{Channel(color.G):X2}{Channel(color.B):X2}";
    }
}

/// <param name="RoleText">
/// リザルト画面に出したのと同じ役職の表示名。スプライトタグや色タグを含む。
/// </param>
/// <param name="TaskText">
/// リザルト画面に出したのと同じタスクの進み具合。色タグを含む。タスクを持たない場合は空。
/// </param>
/// <param name="MoreInformation">
/// 役職やアビリティが持つ追加情報。スプライトタグや色タグを含む。無ければ空。
/// </param>
/// <remarks>
/// <paramref name="RoleText"/> はプレイ時の言語で焼き付いたものになります。
/// 後から言語を変えても訳し直せませんが、役職の変遷やラバーの印まで含めて当時の見え方を残せるため、
/// これで良いものとしています。言語に依らない情報が要るときは <paramref name="RoleId"/> を使ってください。
/// </remarks>
public record ArchivedPlayerResult(byte PlayerId, string Name, bool IsHost, bool IsDead, bool IsDisconnected, string RoleId, IReadOnlyList<string> ModifierIds, string StateTranslationKey, string? StateExtraText, byte? KillerId, ArchivedPlayerColor? Color, string? RoleText = null, string? TaskText = null, string? MoreInformation = null)
{
    public string Name { get; init; } = Name ?? "";
    public string RoleId { get; init; } = RoleId ?? "";
    public IReadOnlyList<string> ModifierIds { get; init; } = ModifierIds ?? [];
    public string StateTranslationKey { get; init; } = StateTranslationKey ?? "";

    /// <param name="color">このプレイヤーの配色。取得できない場合はnull。</param>
    /// <param name="roleText">リザルト画面と同じ役職の表示名。取得できない場合はnull。</param>
    /// <param name="taskText">リザルト画面と同じタスクの進み具合。取得できない場合はnull。</param>
    /// <param name="moreInformation">役職やアビリティが持つ追加情報。取得できない場合はnull。</param>
    public static ArchivedPlayerResult FromPlayer(Player player, ArchivedColor? color = null, string? roleText = null, string? taskText = null, string? moreInformation = null)
    {
        var extra = player.PlayerStateExtraInfo != null && player.PlayerStateExtraInfo.State == player.PlayerState ? player.PlayerStateExtraInfo.ToStateText() : null;

        return new ArchivedPlayerResult(
            player.PlayerId,
            player.Name,
            player.AmHost,
            player.IsDead,
            player.IsDisconnected,
            player.Role?.Assignable.InternalName ?? "",
            player.Modifiers?.Select(m => m.Assignable.InternalName).ToArray() ?? [],
            player.PlayerState?.TranslationKey ?? "",
            extra,
            player.MyKiller?.PlayerId,
            color != null ? ArchivedPlayerColor.From(color) : null,
            roleText,
            taskText,
            moreInformation);
    }
}

/// <summary>
/// フェーズ内で起きたことの理由を、後から読み返せる形で表します。
/// </summary>
/// <param name="TranslationKey">理由の翻訳キー。</param>
/// <param name="Players">その理由が当てはまるプレイヤーのID。</param>
public record ArchivedGameEndReason(string TranslationKey, IReadOnlyList<byte> Players)
{
    public string TranslationKey { get; init; } = TranslationKey ?? "";
    public IReadOnlyList<byte> Players { get; init; } = Players ?? [];
}

/// <summary>
/// 勝敗判定の一フェーズを、後から読み返せる形で表します。
/// </summary>
/// <param name="NameTranslationKey">フェーズ名の翻訳キー。</param>
/// <param name="Winners">このフェーズを終えた時点での勝者のプレイヤーID。</param>
/// <param name="Reasons">このフェーズで起きたことの理由。</param>
public record ArchivedGameEndPhase(string NameTranslationKey, IReadOnlyList<byte> Winners, IReadOnlyList<ArchivedGameEndReason> Reasons)
{
    public string NameTranslationKey { get; init; } = NameTranslationKey ?? "";
    public IReadOnlyList<byte> Winners { get; init; } = Winners ?? [];
    public IReadOnlyList<ArchivedGameEndReason> Reasons { get; init; } = Reasons ?? [];
}

public record ArchivedGameEndStage(string EndConditionId, GameEndReason EndReason, IReadOnlyList<byte> Winners, IReadOnlyList<string> ExtraWinIds, IReadOnlyList<ArchivedGameEndPhase> Phases)
{
    public string EndConditionId { get; init; } = EndConditionId ?? "";
    public IReadOnlyList<byte> Winners { get; init; } = Winners ?? [];
    public IReadOnlyList<string> ExtraWinIds { get; init; } = ExtraWinIds ?? [];
    public IReadOnlyList<ArchivedGameEndPhase> Phases { get; init; } = Phases ?? [];
}

public record ArchivedGameEnd(IReadOnlyList<ArchivedGameEndStage> Stages)
{
    public IReadOnlyList<ArchivedGameEndStage> Stages { get; init; } = Stages ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public ArchivedGameEndStage Original => Stages[0];

    [System.Text.Json.Serialization.JsonIgnore]
    public ArchivedGameEndStage Final => Stages[Stages.Count - 1];

    public static ArchivedGameEnd FromEndState(EndState endState, IEnumerable<Player> players)
    {
        var allPlayers = players.ToArray();

        IReadOnlyList<byte> WinnerIds(BitMask<Player>? mask) =>
            mask == null ? [] : allPlayers.Where(p => mask.Test(p)).Select(p => p.PlayerId).ToArray();

        return new ArchivedGameEnd(endState.Stages.Select(stage => new ArchivedGameEndStage(
            stage.EndCondition.ImmutableId,
            stage.EndReason,
            WinnerIds(stage.Winners),
            ExtraWin.AllExtraWins.Where(w => stage.ExtraWins.Test(w)).Select(w => w.ImmutableId).ToArray(),
            stage.Detail?.Phases
                .Select(phase => new ArchivedGameEndPhase(
                    phase.Name?.TranslationKey ?? "",
                    WinnerIds(phase.Winners),
                    phase.Reasons.Select(r => new ArchivedGameEndReason(r.reason?.TranslationKey ?? "", WinnerIds(r.players))).ToArray()))
                .ToArray() ?? []
            )).ToArray());
    }
}

/// <summary>
/// 記録した瞬間のプレイヤーの状態。
/// </summary>
[Flags]
public enum ArchivedMovementState
{
    None = 0,

    /// <summary>死亡している。切断も死亡として扱われる。</summary>
    Dead = 1,

    /// <summary>
    /// 姿が見えない状態にある。
    /// </summary>
    /// <remarks>
    /// 記録した端末から見た可視性なので、同じゲームでも端末によって値が変わる。
    /// 以降の状態は端末に依らない。
    /// </remarks>
    Invisible = 2,

    /// <summary>ベントの中にいる。</summary>
    InVent = 4,

    /// <summary>地中に潜っている。</summary>
    Dived = 8,

    /// <summary>吹き飛ばされている。</summary>
    Blown = 16,

    /// <summary>はしごや動く床で運ばれている。自力で動いていない。</summary>
    Riding = 32,
}

/// <summary>
/// プレイヤー1人ぶんの、あるタスクフェイズ中の足取りを表します。
/// </summary>
/// <param name="PlayerId">対象のプレイヤー。</param>
/// <param name="Points">
/// 座標を x, y, x, y… の順に並べたもの。小数第1位まで。
/// i 番目の点は <c>Points[i * 2]</c> と <c>Points[i * 2 + 1]</c> です。
/// </param>
/// <param name="States">
/// 各点での状態。<see cref="ArchivedMovementState"/> の値を並べたもの。
/// </param>
public record ArchivedMovementTrack(byte PlayerId, IReadOnlyList<float> Points, IReadOnlyList<int> States)
{
    public IReadOnlyList<float> Points { get; init; } = Points ?? [];
    public IReadOnlyList<int> States { get; init; } = States ?? [];

    /// <summary>記録した点の数。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Count => States.Count;

    /// <summary>i 番目の点での状態。</summary>
    public ArchivedMovementState StateAt(int index) =>
        index < 0 || index >= States.Count ? ArchivedMovementState.None : (ArchivedMovementState)States[index];
}

/// <summary>
/// ニセモノ1体ぶんの足取りを表します。
/// </summary>
/// <remarks>
/// ニセモノは途中で湧いて途中で消えるので、本物と違って記録が区切りの全体には及びません。
/// <paramref name="StartIndex"/> が、その区切りの何点目から現れたかを表します。
/// </remarks>
/// <param name="FakeId">ニセモノの識別子。本物のプレイヤーIDとは重なりません。</param>
/// <param name="OwnerId">呼び出したプレイヤー。分からない場合は null。</param>
/// <param name="ReasonTranslationKey">湧いた理由の翻訳キー。無ければ空。</param>
/// <param name="Color">見た目の配色。取得できない場合は null。</param>
/// <param name="StartIndex">その区切りの何点目から記録が始まるか。</param>
public record ArchivedFakeTrack(int FakeId, byte? OwnerId, string ReasonTranslationKey, ArchivedPlayerColor? Color, int StartIndex, IReadOnlyList<float> Points, IReadOnlyList<int> States)
{
    public string ReasonTranslationKey { get; init; } = ReasonTranslationKey ?? "";
    public IReadOnlyList<float> Points { get; init; } = Points ?? [];
    public IReadOnlyList<int> States { get; init; } = States ?? [];

    /// <summary>記録した点の数。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Count => States.Count;
}

/// <summary>
/// タスクフェイズ1回ぶんの、全プレイヤーの足取りを表します。
/// </summary>
/// <param name="StartTime">計測を始めたときのゲーム内時刻(秒)。</param>
/// <param name="Interval">
/// 記録の間隔(秒)。i 番目の点の時刻は <c>StartTime + Interval * i</c> です。
/// </param>
/// <param name="Tracks">プレイヤーごとの足取り。点の数はどれも同じになります。</param>
/// <param name="FakeTracks">ニセモノごとの足取り。湧き消えするので点の数はまちまちです。</param>
public record ArchivedMovementPhase(float StartTime, float Interval, IReadOnlyList<ArchivedMovementTrack> Tracks, IReadOnlyList<ArchivedFakeTrack>? FakeTracks = null)
{
    public IReadOnlyList<ArchivedMovementTrack> Tracks { get; init; } = Tracks ?? [];
    public IReadOnlyList<ArchivedFakeTrack> FakeTracks { get; init; } = FakeTracks ?? [];
}

/// <summary>
/// イベントが起きた瞬間の、あるプレイヤーの居場所。
/// </summary>
public record ArchivedEventPosition(byte PlayerId, float X, float Y);

/// <summary>
/// ゲーム中に起きた出来事ひとつを、後から読み返せる形で表します。
/// </summary>
/// <param name="VariationId">出来事の種類。<c>GameStatistics.EventVariation</c> の Id。</param>
/// <param name="Time">起きたときのゲーム内時刻(秒)。</param>
/// <param name="SourceId">引き起こした側のプレイヤー。いなければ null。</param>
/// <param name="TargetIds">巻き込まれた側のプレイヤー。</param>
/// <param name="DetailTranslationKey">内容を表す翻訳キー。無ければ空。</param>
/// <param name="Positions">
/// そのときの各プレイヤーの居場所。種類によっては記録しないので、その場合は空。
/// </param>
public record ArchivedGameEventRecord(int VariationId, float Time, byte? SourceId, IReadOnlyList<byte> TargetIds, string DetailTranslationKey, IReadOnlyList<ArchivedEventPosition> Positions)
{
    public IReadOnlyList<byte> TargetIds { get; init; } = TargetIds ?? [];
    public string DetailTranslationKey { get; init; } = DetailTranslationKey ?? "";
    public IReadOnlyList<ArchivedEventPosition> Positions { get; init; } = Positions ?? [];

    public static ArchivedGameEventRecord FromEvent(IArchivedEvent source) => new(
        source.EventVariation.Id,
        source.Time,
        source.SourceId,
        TargetsOf(source.TargetIdMask),
        source.RelatedTag?.TranslationKey ?? "",
        source.Position.Select(p => new ArchivedEventPosition(p.Item1, p.Item2.x, p.Item2.y)).ToArray());

    private static byte[] TargetsOf(int mask)
    {
        var targets = new List<byte>();
        for (byte id = 0; id < 32; id++) if ((mask & (1 << id)) != 0) targets.Add(id);
        return [.. targets];
    }
}

public interface IArchivedGameData
{
    byte MapId { get; }
    DateTime? StartedAtUtc { get; }
    DateTime? EndedAtUtc { get; }
    ArchivedGameEnd? EndInfo { get; }
    IReadOnlyList<ArchivedPlayerResult> PlayerResults { get; }

    /// <summary>タスクフェイズごとのプレイヤーの足取り。古い順。</summary>
    IReadOnlyList<ArchivedMovementPhase> MovementPhases { get; }

    /// <summary>ゲーム中に起きた出来事。古い順。</summary>
    IReadOnlyList<ArchivedGameEventRecord> Events { get; }
}

public interface IArchivedGame : IArchivedGameData
{
    internal IReadOnlyList<RoleHistory> RoleHistory { get; }
    IArchivedPlayer? GetPlayer(byte playerId);
    IEnumerable<IArchivedPlayer> GetAllPlayers();
    IArchivedEvent[] ArchivedEvents { get; }
    ArchivedColor GetColor(byte colorId);
}

public interface IArchivedEventVariation
{
    int Id { get; }
    Media.Image? EventIcon { get; }
    Media.Image? InteractionIcon { get; }
    bool ShowPlayerPosition { get; }
    bool CanCombine { get; }
}
public interface IArchivedEvent
{
    IArchivedEventVariation EventVariation { get; }
    public float Time { get; }
    public byte? SourceId { get; }
    public int TargetIdMask { get; }
    public Tuple<byte, Virial.Compat.Vector2>[] Position { get; }
    public CommunicableTextTag? RelatedTag { get; }
}

internal record RoleHistory
{
    public float Time;
    public byte PlayerId;
    public bool IsModifier;
    public bool IsSet;
    public bool Dead;

    public RuntimeAssignable Assignable;

    public RoleHistory(float time, byte playerId, RuntimeModifier modifier, bool isSet, bool dead)
    {
        Time = time;
        PlayerId = playerId;
        IsModifier = true;
        IsSet = isSet;
        Assignable = modifier;
        Dead = dead;
    }

    public RoleHistory(float time, byte playerId, RuntimeRole role, bool dead)
    {
        Time = time;
        PlayerId = playerId;
        IsModifier = false;
        IsSet = true;
        Assignable = role;
        Dead = dead;
    }

    public RoleHistory(float time, byte playerId, RuntimeGhostRole role, bool dead)
    {
        Time = time;
        PlayerId = playerId;
        IsModifier = false;
        IsSet = true;
        Assignable = role;
        Dead = dead;
    }
}
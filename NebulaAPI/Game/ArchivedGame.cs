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
    Dead = 1,
    Invisible = 2,
    InVent = 4,
    Dived = 8,
    Blown = 16,
    Riding = 32, // はしご、ぬーん使用中
}

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
/// <param name="InitialObjectIds">
/// この区切りが始まった時点で残っていたマップオブジェクトの識別子。
/// 時刻からも割り出せますが、ターン単位で見るときに全体を辿らずに済むよう持たせています。
/// </param>
public record ArchivedMovementPhase(float StartTime, float Interval, IReadOnlyList<ArchivedMovementTrack> Tracks, IReadOnlyList<ArchivedFakeTrack>? FakeTracks = null, IReadOnlyList<int>? InitialObjectIds = null)
{
    public IReadOnlyList<ArchivedMovementTrack> Tracks { get; init; } = Tracks ?? [];
    public IReadOnlyList<ArchivedFakeTrack> FakeTracks { get; init; } = FakeTracks ?? [];
    public IReadOnlyList<int> InitialObjectIds { get; init; } = InitialObjectIds ?? [];
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

/// <summary>
/// 動くマップオブジェクトが、ある時点にいた場所。
/// </summary>
public record ArchivedVoteCast(byte VoterId, byte VotedForId);

public record ArchivedVoteSwap(byte From, byte To);

/// <summary>
/// 1回の投票の結果。
/// </summary>
/// <param name="Time">結果が開示されたゲーム内時刻(秒)。</param>
/// <param name="Votes">すり替え前の票。重みのぶんだけ同じ投票者が並びます。投票者が255の票は同数時の追加票です。</param>
/// <param name="Swaps">投票先のすり替え。すり替え後の票はここから割り出せます。</param>
public record ArchivedVoteResult(float Time, IReadOnlyList<ArchivedVoteCast> Votes, IReadOnlyList<ArchivedVoteSwap> Swaps)
{
    public IReadOnlyList<ArchivedVoteCast> Votes { get; init; } = Votes ?? [];
    public IReadOnlyList<ArchivedVoteSwap> Swaps { get; init; } = Swaps ?? [];
}

public record ArchivedMapObjectMove(float Time, float X, float Y);

/// <summary>
/// マップに現れた物の、その時々のありさま。
/// </summary>
/// <remarks>
/// 1つの整数に詰めています。下位1ビットが見え方、その上が絵の段です。
/// どこを使うかは、その種類が持つ性質で決まります。
/// </remarks>
public static class ArchivedMapObjectState
{
    /// <summary>置いた本人以外には見えていない、を表す位。</summary>
    public const int Hidden = 1;

    /// <summary>絵の段が始まる位。</summary>
    public const int StageShift = 1;

    public static bool IsHidden(int state) => (state & Hidden) != 0;

    /// <summary>何枚目の絵で描くか。0 が既定。</summary>
    public static int StageOf(int state) => state >> StageShift;

    public static int Of(bool hidden, int stage) => (hidden ? Hidden : 0) | (stage << StageShift);
}

/// <summary>ありさまが変わった瞬間。</summary>
public record ArchivedMapObjectChange(float Time, int State);

/// <summary>
/// マップ上に現れたオブジェクトの記録。
/// </summary>
/// <param name="ObjectId">この物の識別子。</param>
/// <param name="OwnerId">持ち主のプレイヤーID。</param>
/// <param name="KindId">種類。アイコンや性質はこの名前から引きます。</param>
/// <param name="SpawnTime">現れたときのゲーム内時刻(秒)。</param>
/// <param name="DespawnTime">消えたときのゲーム内時刻(秒)。残ったままなら null。</param>
/// <param name="X">現れた場所。</param>
/// <param name="Y">現れた場所。</param>
/// <param name="VelocityX">等速で動く物の速度。それ以外は 0。</param>
/// <param name="VelocityY">等速で動く物の速度。それ以外は 0。</param>
/// <param name="Angle">向きを持つ物の角度(度)。それ以外は 0。</param>
/// <param name="State">現れたときのありさま。</param>
/// <param name="Changes">ありさまが変わった記録。変わったときだけ点が増えます。</param>
/// <param name="FlipX">左右反転して描くか。</param>
/// <param name="FlipY">上下反転して描くか。</param>
/// <param name="Moves">動きうる対象。動いたときだけ点が増えます。</param>
public record ArchivedMapObject(
    int ObjectId, string KindId, float SpawnTime, float? DespawnTime,
    float X, float Y, float VelocityX, float VelocityY, float Angle,
    bool FlipX, bool FlipY, IReadOnlyList<ArchivedMapObjectMove> Moves,
    int State = 0, IReadOnlyList<ArchivedMapObjectChange>? Changes = null, byte? OwnerId = null)
{
    public string KindId { get; init; } = KindId ?? "";
    public IReadOnlyList<ArchivedMapObjectMove> Moves { get; init; } = Moves ?? [];
    public IReadOnlyList<ArchivedMapObjectChange> Changes { get; init; } = Changes ?? [];
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

    /// <summary>マップ上に現れた物。現れた順。</summary>
    IReadOnlyList<ArchivedMapObject> MapObjects { get; }

    /// <summary>投票の結果。古い順。</summary>
    IReadOnlyList<ArchivedVoteResult> VoteResults { get; }
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
    bool IsShownInGame { get; }
    bool IsTrivial { get; }
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
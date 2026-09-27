using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nebula.Game.Statistics;
using Virial.Game;

namespace Nebula.Http;

internal static class GameRecordView
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string SerializeList(IEnumerable<StoredGameRecord> stored) =>
        JsonSerializer.Serialize(stored.Select(SummaryOf).ToArray(), SerializerOptions);

    public static string Serialize(StoredGameRecord stored) =>
        JsonSerializer.Serialize(DetailOf(stored), SerializerOptions);

    // ------------------------------------------------------------------ 組み立て

    private static GameSummaryView SummaryOf(StoredGameRecord stored)
    {
        var record = stored.Record;
        var finalStage = record.EndInfo?.Stages.LastOrDefault();

        return new GameSummaryView
        {
            Id = record.Id,
            StartedAt = record.StartedAtUtc,
            EndedAt = record.EndedAtUtc,
            Marked = stored.Marked,
            MapName = MapName(record.MapId),
            ConditionText = finalStage != null ? ConditionText(finalStage) : "",
        };
    }

    private static GameDetailView DetailOf(StoredGameRecord stored)
    {
        var record = stored.Record;

        return new GameDetailView
        {
            Id = record.Id,
            Version = record.Version,
            StartedAt = record.StartedAtUtc,
            EndedAt = record.EndedAtUtc,
            Marked = stored.Marked,
            MapId = record.MapId,
            MapName = MapName(record.MapId),
            Players = record.PlayerResults.Select(PlayerOf).ToArray(),
            Movement = record.Movement,
            FakeReasons = FakeReasonsOf(record),
            Events = record.Events.Select(EventOf).ToArray(),
            MapObjects = record.MapObjects.Select(MapObjectOf).ToArray(),
            Votes = record.VoteResults.Select(VoteOf).ToArray(),
            TimeOrigin = TimeOf(record, GameStatistics.EventVariation.GameStart.Id, first: true),
            TimeEnd = TimeOf(record, GameStatistics.EventVariation.GameEnd.Id, first: false),
            End = record.EndInfo != null ? new GameEndView { Stages = record.EndInfo.Stages.Select(StageOf).ToArray() } : null,
        };
    }

    private static PlayerView PlayerOf(ArchivedPlayerResult player) => new()
    {
        PlayerId = player.PlayerId,
        Name = player.Name,
        IsHost = player.IsHost,
        IsDead = player.IsDead,
        IsDisconnected = player.IsDisconnected,
        Role = RoleName(player.RoleId),
        RoleHtml = RichTextHtml.ToHtml(player.RoleText),
        TaskHtml = RichTextHtml.ToHtml(player.TaskText),
        MoreHtml = RichTextHtml.ToHtml(player.MoreInformation),
        State = Translate(player.StateTranslationKey),
        StateExtra = Plain(player.StateExtraText),
        KillerId = player.KillerId,
        Color = player.Color?.Main,
        ShadowColor = player.Color?.Shadow,
        VisorColor = player.Color?.Visor,
    };

    private static StageView StageOf(ArchivedGameEndStage stage) => new()
    {
        Condition = stage.EndConditionId,
        ConditionText = ConditionText(stage),
        Reason = stage.EndReason.ToString(),
        ReasonText = ReasonText(stage.EndReason),
        Winners = stage.Winners.Select(id => (int)id).ToArray(),
        ExtraWins = stage.ExtraWinIds.ToArray(),
        ExtraWinTexts = stage.ExtraWinIds.Select(ExtraWinText).ToArray(),
        Phases = stage.Phases.Select(phase => new PhaseView
        {
            Name = phase.NameTranslationKey,
            NameText = Translate(phase.NameTranslationKey),
            Winners = phase.Winners.Select(id => (int)id).ToArray(),
            Reasons = phase.Reasons.Select(reason => new ReasonView
            {
                Reason = reason.TranslationKey,
                ReasonText = Translate(reason.TranslationKey),
                Players = reason.Players.Select(id => (int)id).ToArray(),
            }).ToArray(),
        }).ToArray(),
    };

    private static string Plain(string? text) => System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^<>]*>", "").Trim();

    private static string Translate(string? key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        return Plain(Language.TryTranslate(key, out var translated) ? translated : key);
    }

    /// <summary>
    /// ゲームの開始あるいは終了にあたる時刻を探す。
    /// </summary>
    /// <remarks>
    /// 記録している時刻は <c>NebulaGameManager.CurrentTime</c> で、これはゲーム開始より前から進んでいる。
    /// 経過時間として見せるには、開始の出来事を原点に取り直す必要がある。
    /// 目印の出来事が無い記録では、記録してある出来事の端で代用する。
    /// </remarks>
    private static float TimeOf(GameRecord record, int variationId, bool first)
    {
        var marker = first
            ? record.Events.FirstOrDefault(e => e.VariationId == variationId)
            : record.Events.LastOrDefault(e => e.VariationId == variationId);
        if (marker != null) return marker.Time;

        if (record.Events.Count == 0) return 0f;
        return first ? record.Events.Min(e => e.Time) : record.Events.Max(e => e.Time);
    }

    /// <summary>
    /// 足取りに出てくるニセモノの、湧いた理由を訳したもの。
    /// </summary>
    /// <remarks>
    /// 足取りそのものは圧縮したまま閲覧画面へ渡すので、中の翻訳キーは訳せない。
    /// そこで、出てくる分だけを訳して対応表として添える。
    /// </remarks>
    private static Dictionary<string, string> FakeReasonsOf(GameRecord record)
    {
        var reasons = new Dictionary<string, string>();

        foreach (var phase in record.MovementPhases)
        {
            foreach (var fake in phase.FakeTracks)
            {
                if (fake.ReasonTranslationKey.Length == 0) continue;
                if (reasons.ContainsKey(fake.ReasonTranslationKey)) continue;

                reasons[fake.ReasonTranslationKey] = Translate(fake.ReasonTranslationKey);
            }
        }

        return reasons;
    }

    private static GameEventView EventOf(ArchivedGameEventRecord source) => new()
    {
        Segment = SegmentOf(source.VariationId),
        Trivial = GameStatistics.EventVariation.TryValueOf(source.VariationId)?.IsTrivial ?? false,
        Time = source.Time,
        SourceId = source.SourceId,
        TargetIds = source.TargetIds.Select(id => (int)id).ToArray(),
        Detail = source.DetailTranslationKey,
        DetailText = Translate(source.DetailTranslationKey),
        Positions = source.Positions.Select(p => new EventPositionView { PlayerId = p.PlayerId, X = p.X, Y = p.Y }).ToArray(),
    };

    /// <summary>
    /// マップに現れた物 1 つ。
    /// </summary>
    /// <remarks>
    /// 足取りは要素の多い配列なので、時刻・X・Y を並べた平らな配列にして持たせる。
    /// 動かない物ではここが空になる。
    /// </remarks>
    private static MapObjectView MapObjectOf(ArchivedMapObject source)
    {
        var moves = new float[source.Moves.Count * 3];
        for (int i = 0; i < source.Moves.Count; i++)
        {
            moves[i * 3] = source.Moves[i].Time;
            moves[i * 3 + 1] = source.Moves[i].X;
            moves[i * 3 + 2] = source.Moves[i].Y;
        }

        //ありさまも時刻と値を並べた平らな配列にする。
        var changes = new float[source.Changes.Count * 2];
        for (int i = 0; i < source.Changes.Count; i++)
        {
            changes[i * 2] = source.Changes[i].Time;
            changes[i * 2 + 1] = source.Changes[i].State;
        }

        return new MapObjectView
        {
            ObjectId = source.ObjectId,
            Kind = source.KindId,
            Spawn = source.SpawnTime,
            Despawn = source.DespawnTime,
            X = source.X,
            Y = source.Y,
            Vx = source.VelocityX,
            Vy = source.VelocityY,
            Angle = source.Angle,
            FlipX = source.FlipX,
            FlipY = source.FlipY,
            Moves = moves,
            State = source.State,
            Changes = changes,
            OwnerId = source.OwnerId,
        };
    }

    /// <summary>
    /// 出来事がターンの区切りにどう関わるか。閲覧画面へは番号ではなくこの意味で渡す。
    /// </summary>
    /// <remarks>知らない番号の出来事は、区切りには関わらないものとして扱う。</remarks>
    private static string SegmentOf(int variationId) =>
        (GameStatistics.EventVariation.TryValueOf(variationId)?.Segment ?? EventSegment.None) switch
        {
            EventSegment.TurnStart => "turn",
            EventSegment.MeetingStart => "meeting",
            EventSegment.GameEnd => "gameEnd",
            _ => "",
        };

    private static VoteResultView VoteOf(ArchivedVoteResult source)
    {
        var votes = new int[source.Votes.Count * 2];
        for (int i = 0; i < source.Votes.Count; i++)
        {
            votes[i * 2] = source.Votes[i].VoterId;
            votes[i * 2 + 1] = source.Votes[i].VotedForId;
        }

        var swaps = new int[source.Swaps.Count * 2];
        for (int i = 0; i < source.Swaps.Count; i++)
        {
            swaps[i * 2] = source.Swaps[i].From;
            swaps[i * 2 + 1] = source.Swaps[i].To;
        }

        return new VoteResultView { Time = source.Time, Votes = votes, Swaps = swaps };
    }

    private static string MapName(byte mapId)
    {
        try
        {
            return Plain(AmongUsUtil.ToLocalizedMapName(mapId));
        }
        catch (Exception)
        {
            return mapId.ToString();
        }
    }

    private static string RoleName(string internalName) =>
        string.IsNullOrEmpty(internalName) ? "" : Translate("role." + internalName + ".name");

    private static string ReasonText(GameEndReason reason) =>
        Translate("end.reason." + reason.ToString().HeadLower());

    private static string ConditionText(ArchivedGameEndStage stage)
    {
        var gameEnd = GameEnd.AllEndConditions.FirstOrDefault(e => e.ImmutableId == stage.EndConditionId);
        if (gameEnd == null) return stage.EndConditionId;

        var extra = string.Concat(stage.ExtraWinIds.Select(RawExtraWinText));
        return Plain(gameEnd.DisplayText.GetString().Replace("%EXTRA%", extra));
    }

    private static string RawExtraWinText(string immutableId) =>
        ExtraWin.AllExtraWins.FirstOrDefault(w => w.ImmutableId == immutableId)?.DisplayText.GetString() ?? immutableId;

    private static string ExtraWinText(string immutableId) => Plain(RawExtraWinText(immutableId));
}

// ---------------------------------------------------------------------- 応答の形

internal sealed class GameSummaryView
{
    public string Id { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool Marked { get; set; }
    public string MapName { get; set; } = "";
    public string ConditionText { get; set; } = "";
}

internal sealed class GameDetailView
{
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool Marked { get; set; }
    public byte MapId { get; set; }
    public string MapName { get; set; } = "";
    public PlayerView[] Players { get; set; } = [];
    public GameEndView? End { get; set; }

    /// <summary>
    /// タスクフェイズごとの足取り。GZip で圧縮したものを Base64 にした文字列。
    /// </summary>
    /// <remarks>
    /// 展開すると数百 KB になるので、圧縮したまま渡して閲覧画面で開かせる。
    /// ブラウザの <c>DecompressionStream("gzip")</c> がそのまま解ける。
    /// </remarks>
    public string? Movement { get; set; }

    /// <summary>ゲーム中に起きた出来事。古い順。</summary>
    public GameEventView[] Events { get; set; } = [];

    /// <summary>マップ上に現れた物。現れた順。</summary>
    public MapObjectView[] MapObjects { get; set; } = [];

    /// <summary>投票の結果。古い順。</summary>
    public VoteResultView[] Votes { get; set; } = [];

    /// <summary>足取りに出てくるニセモノの、湧いた理由。翻訳キー → 訳文。</summary>
    public Dictionary<string, string> FakeReasons { get; set; } = [];

    /// <summary>
    /// ゲームが始まった瞬間の時刻。経過時間はここを 0 として数える。
    /// </summary>
    /// <remarks>
    /// 記録中の時刻はゲーム開始より前から進んでいるので、そのままでは経過時間にならない。
    /// </remarks>
    public float TimeOrigin { get; set; }

    /// <summary>ゲームが終わった瞬間の時刻。</summary>
    public float TimeEnd { get; set; }
}

internal sealed class PlayerView
{
    public byte PlayerId { get; set; }
    public string Name { get; set; } = "";
    public bool IsHost { get; set; }
    public bool IsDead { get; set; }
    public bool IsDisconnected { get; set; }
    public string Role { get; set; } = "";

    /// <summary>
    /// リザルト画面と同じ役職の表示名をHTMLにしたもの。記録に無ければ空。
    /// </summary>
    /// <remarks>
    /// 記録した時点の言語で焼き付いている。閲覧時の言語では訳し直せないので、
    /// 空のときは <see cref="Role"/> （翻訳し直せる役職名）を代わりに出すこと。
    /// </remarks>
    public string RoleHtml { get; set; } = "";

    /// <summary>リザルト画面と同じタスクの進み具合をHTMLにしたもの。タスクが無ければ空。</summary>
    public string TaskHtml { get; set; } = "";

    /// <summary>役職やアビリティが持つ追加情報をHTMLにしたもの。無ければ空。</summary>
    public string MoreHtml { get; set; } = "";
    public string State { get; set; } = "";
    public string? StateExtra { get; set; }
    public byte? KillerId { get; set; }

    /// <summary>体の色。#RRGGBB。</summary>
    public string? Color { get; set; }
    /// <summary>影の色。#RRGGBB。</summary>
    public string? ShadowColor { get; set; }
    /// <summary>バイザーの色。#RRGGBB。</summary>
    public string? VisorColor { get; set; }
}

internal sealed class GameEventView
{
    /// <summary>
    /// ターンの区切りにどう関わるか。"turn" / "meeting" / "gameEnd"、関わらなければ空。
    /// </summary>
    public string Segment { get; set; } = "";

    public bool Trivial { get; set; }

    /// <summary>ゲーム内時刻(秒)。</summary>
    public float Time { get; set; }

    public byte? SourceId { get; set; }

    /// <remarks>byte[] にすると System.Text.Json が Base64 文字列にしてしまうので int[] で持つ。</remarks>
    public int[] TargetIds { get; set; } = [];

    public string Detail { get; set; } = "";
    public string DetailText { get; set; } = "";

    /// <summary>そのときの各プレイヤーの居場所。記録していなければ空。</summary>
    public EventPositionView[] Positions { get; set; } = [];
}

internal sealed class MapObjectView
{
    public int ObjectId { get; set; }

    /// <summary>種類。絵と性質は <c>/api/map-objects</c> の一覧から引く。</summary>
    public string Kind { get; set; } = "";

    public float Spawn { get; set; }

    /// <summary>消えた時刻。残ったままなら null。</summary>
    public float? Despawn { get; set; }

    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>等速で動く物の速度。それ以外は 0。</summary>
    public float Vx { get; set; }
    public float Vy { get; set; }

    /// <summary>向きを持つ物の角度(度)。</summary>
    public float Angle { get; set; }

    public bool FlipX { get; set; }
    public bool FlipY { get; set; }

    /// <summary>動きうる物の足取り。時刻・X・Y の繰り返し。</summary>
    public float[] Moves { get; set; } = [];

    /// <summary>現れたときのありさま。</summary>
    public int State { get; set; }

    /// <summary>ありさまが変わった記録。時刻・値の繰り返し。</summary>
    public float[] Changes { get; set; } = [];

    /// <summary>持ち主のプレイヤーID。付いて回る物では、この人の足取りが居場所になる。</summary>
    public byte? OwnerId { get; set; }
}

internal sealed class VoteResultView
{
    public float Time { get; set; }

    /// <summary>投票者・投票先の繰り返し。投票者が255の票は同数時の追加票。</summary>
    public int[] Votes { get; set; } = [];

    /// <summary>すり替え元・すり替え先の繰り返し。</summary>
    public int[] Swaps { get; set; } = [];
}

internal sealed class EventPositionView
{
    public byte PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

internal sealed class GameEndView
{
    public StageView[] Stages { get; set; } = [];
}

internal sealed class StageView
{
    public string Condition { get; set; } = "";
    public string ConditionText { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ReasonText { get; set; } = "";
    /// <remarks>byte[] にすると System.Text.Json が Base64 文字列にしてしまうので int[] で持つ。</remarks>
    public int[] Winners { get; set; } = [];
    public string[] ExtraWins { get; set; } = [];
    public string[] ExtraWinTexts { get; set; } = [];
    public PhaseView[] Phases { get; set; } = [];
}

internal sealed class PhaseView
{
    public string Name { get; set; } = "";
    public string NameText { get; set; } = "";

    /// <summary>このフェーズを終えた時点での勝者。</summary>
    /// <remarks>byte[] にすると System.Text.Json が Base64 文字列にしてしまうので int[] で持つ。</remarks>
    public int[] Winners { get; set; } = [];

    public ReasonView[] Reasons { get; set; } = [];
}

internal sealed class ReasonView
{
    public string Reason { get; set; } = "";
    public string ReasonText { get; set; } = "";

    /// <summary>その理由が当てはまるプレイヤー。</summary>
    /// <remarks>byte[] にすると System.Text.Json が Base64 文字列にしてしまうので int[] で持つ。</remarks>
    public int[] Players { get; set; } = [];
}

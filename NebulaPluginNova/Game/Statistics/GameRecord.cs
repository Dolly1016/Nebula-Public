using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Virial.Game;

namespace Nebula.Game.Statistics;

/// <summary>
/// ディスクに保存する1ゲーム分の記録。<see cref="IArchivedGameData"/> の JSON 表現。
/// </summary>
/// <remarks>
/// <para>
/// 保持するのは素のデータだけで、翻訳済みのテキストは一切持たない。
/// 役職は内部名、勝利条件と追加勝利は不変ID、経緯は翻訳キーで持ち、
/// 表示のための翻訳は読み出す側（HTTPの応答を組み立てるところ）で行う。
/// 保存した時点の言語に記録が縛られないようにするため。
/// </para>
/// <para>
/// 項目を増やすときは <see cref="CurrentVersion"/> を上げ、読み手側で分岐できるようにすること。
/// </para>
/// </remarks>
internal sealed class GameRecord : IArchivedGameData
{
    /// <summary>この形式の版番号。</summary>
    public const int CurrentVersion = 13;

    /// <summary>
    /// 読み書きに使う設定。プロパティ名はキャメルケース、列挙子は名前で書く。
    /// </summary>
    /// <remarks>
    /// 整形はしない。足取りのように項目数が多いものでは、改行と字下げだけでファイルが数倍に膨らむため。
    /// </remarks>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public int Version { get; set; } = CurrentVersion;

    /// <summary>記録の識別子。拡張子を除いたファイル名と一致する。</summary>
    public string Id { get; set; } = "";

    public byte MapId { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public ArchivedGameEnd? EndInfo { get; set; }
    public IReadOnlyList<ArchivedPlayerResult> PlayerResults { get; set; } = [];

    /// <summary>
    /// タスクフェイズごとのプレイヤーの足取りを、JSON にして圧縮したもの。
    /// </summary>
    /// <remarks>
    /// 0.25 秒ごとの点が人数ぶん並ぶため、そのまま書くと記録 1 件で数百 KB になる。
    /// ここだけ圧縮して 1 個の文字列に畳んでおき、読むときに <see cref="MovementPhases"/> で開く。
    /// </remarks>
    public string? Movement { get; set; }

    /// <summary>ゲーム中に起きた出来事。古い順。</summary>
    public IReadOnlyList<ArchivedGameEventRecord> Events { get; set; } = [];

    private IReadOnlyList<ArchivedMovementPhase>? movementPhases = null;

    /// <summary>タスクフェイズごとのプレイヤーの足取り。古い順。</summary>
    [JsonIgnore]
    public IReadOnlyList<ArchivedMovementPhase> MovementPhases
    {
        get => movementPhases ??= CompressedJson.Decompress<ArchivedMovementPhase[]>(Movement, SerializerOptions) ?? [];
        set
        {
            movementPhases = value;
            Movement = CompressedJson.Compress(value, SerializerOptions);
        }
    }

    public static GameRecord From(string id, IArchivedGameData game) => new()
    {
        Version = CurrentVersion,
        Id = id,
        MapId = game.MapId,
        StartedAtUtc = ToUtc(game.StartedAtUtc),
        EndedAtUtc = ToUtc(game.EndedAtUtc),
        EndInfo = game.EndInfo,
        PlayerResults = game.PlayerResults.ToArray(),
        Movement = CompressedJson.Compress(game.MovementPhases.ToArray(), SerializerOptions),
        Events = game.Events.ToArray(),
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static GameRecord? FromJson(string json) =>
        JsonSerializer.Deserialize<GameRecord>(json, SerializerOptions);

    /// <summary>
    /// System.Text.Json が末尾に Z を付けて書き出すよう、Kind を Utc に揃える。
    /// </summary>
    private static DateTime? ToUtc(DateTime? time)
    {
        if (!time.HasValue) return null;
        return time.Value.Kind switch
        {
            DateTimeKind.Utc => time.Value,
            DateTimeKind.Local => time.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(time.Value, DateTimeKind.Utc),
        };
    }
}

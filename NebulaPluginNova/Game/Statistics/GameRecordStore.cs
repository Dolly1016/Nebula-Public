using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nebula.Modules;
using Virial;
using Virial.Game;

namespace Nebula.Game.Statistics;

/// <summary>
/// ゲーム記録をディスク上に保存し、読み出す。
/// </summary>
/// <remarks>
/// 保存先は DataSaver と同じ <c>NebulaOnTheShip</c> フォルダの直下。
/// <code>
/// NebulaOnTheShip/
/// └ GameRecords/
///    ├ 20260919T101530Z_a1b2c3d4.json   通常。直近 <see cref="MaxUnmarkedRecords"/> 件だけ残る
///    └ Marked/
///       └ …                              マーク済み。件数制限なし
/// </code>
/// マークされているかどうかはファイルの置き場所だけで決まる。JSON 本体には書かない。
/// ゲーム終了時（メインスレッド）と HTTP サーバー（別スレッド）の双方から触るため、
/// ファイル操作はすべて <see cref="Gate"/> で直列化する。
/// </remarks>
/// <summary>
/// ディスク上に置かれた記録 1 件。
/// </summary>
/// <param name="Record">記録の中身。</param>
/// <param name="Marked">マークされているか。ファイルの置き場所で決まる。</param>
internal record StoredGameRecord(GameRecord Record, bool Marked);

internal static class GameRecordStore
{
    /// <summary>マークされていない記録を残す上限。</summary>
    public const int MaxUnmarkedRecords = 100;

    private static readonly object Gate = new();

    private static readonly Virial.Logging.ILogger Logger =
        NebulaAPI.Logging.NebulaLogger("GameRecord");

    /// <summary>記録の識別子として許す形。パスに使う前に必ず通す。</summary>
    private static readonly Regex ValidId = new(@"\A[A-Za-z0-9_\-]{1,64}\z", RegexOptions.Compiled);

    public static string RootPath =>
        DataSaver.DataSaverFolderPath + Path.DirectorySeparatorChar + "GameRecords";

    public static string MarkedPath =>
        RootPath + Path.DirectorySeparatorChar + "Marked";

    // ------------------------------------------------------------------ 保存

    /// <summary>
    /// 1 ゲーム分を書き出し、あふれた古い記録を削る。
    /// </summary>
    /// <remarks>リザルト画面の構築中に呼ばれるので、失敗しても例外を外に出さない。</remarks>
    public static void Save(IArchivedGameData game)
    {
        try
        {
            var id = GenerateId(game.StartedAtUtc ?? game.EndedAtUtc ?? DateTime.UtcNow);
            var json = GameRecord.From(id, game).ToJson();

            lock (Gate)
            {
                Directory.CreateDirectory(RootPath);
                File.WriteAllText(PathOf(RootPath, id), json, Encoding.UTF8);
                TrimWithinLock(MaxUnmarkedRecords);
            }

            Logger.Message($"Saved a game record. ({id})");
        }
        catch (Exception e)
        {
            Logger.Error("Failed to save the game record.\n" + e.ToString());
        }
    }

    // ------------------------------------------------------------------ 読み出し

    /// <summary>
    /// すべての記録を、新しいものから順に返す。
    /// </summary>
    public static IReadOnlyList<StoredGameRecord> List()
    {
        List<StoredGameRecord> result = [];

        lock (Gate)
        {
            Collect(RootPath, false);
            Collect(MarkedPath, true);
        }

        //開始時刻の降順。時刻を持たないものは末尾へ。
        return result
            .OrderByDescending(s => s.Record.StartedAtUtc ?? DateTime.MinValue)
            .ThenByDescending(s => s.Record.Id, StringComparer.Ordinal)
            .ToArray();

        void Collect(string directory, bool marked)
        {
            foreach (var path in EnumerateRecordFiles(directory))
            {
                var id = Path.GetFileNameWithoutExtension(path);
                var record = Parse(id, File.ReadAllText(path)) ?? new GameRecord { Id = id };
                record.Id = id;
                result.Add(new StoredGameRecord(record, marked));
            }
        }
    }

    /// <summary>
    /// 記録 1 件を返す。見つからなければ null。
    /// </summary>
    public static StoredGameRecord? Read(string id)
    {
        if (!ValidId.IsMatch(id)) return null;

        lock (Gate)
        {
            var path = LocateWithinLock(id);
            if (path == null) return null;

            try
            {
                var record = Parse(id, File.ReadAllText(path));
                if (record == null) return null;
                record.Id = id;
                return new StoredGameRecord(record, IsMarkedPath(path));
            }
            catch (Exception e)
            {
                Logger.Warning($"Failed to read a game record. ({id})\n" + e.Message);
                return null;
            }
        }
    }

    private static GameRecord? Parse(string id, string json)
    {
        try
        {
            return GameRecord.FromJson(json);
        }
        catch (Exception e)
        {
            Logger.Warning($"Failed to read a game record. ({id})\n" + e.Message);
            return null;
        }
    }

    private static bool IsMarkedPath(string path) =>
        string.Equals(Path.GetDirectoryName(path), MarkedPath, StringComparison.OrdinalIgnoreCase);


    // ------------------------------------------------------------------ マーク

    /// <summary>
    /// マークを付け外しする。実体はフォルダ間の移動。
    /// </summary>
    /// <returns>対象が存在し、望む状態になったなら true。</returns>
    public static bool SetMarked(string id, bool marked)
    {
        if (!ValidId.IsMatch(id)) return false;

        lock (Gate)
        {
            var from = PathOf(marked ? RootPath : MarkedPath, id);
            var to = PathOf(marked ? MarkedPath : RootPath, id);

            //すでに望む側にあるなら何もしない。
            if (File.Exists(to) && !File.Exists(from)) return true;
            if (!File.Exists(from)) return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Move(from, to, true);
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to change the mark of a game record. ({id})\n" + e.ToString());
                return false;
            }

            //マークを外した結果あふれることがある。
            if (!marked) TrimWithinLock(MaxUnmarkedRecords);

            return true;
        }
    }

    // ------------------------------------------------------------------ 件数制限

    /// <summary>マークされていない記録を新しいものから <paramref name="max"/> 件だけ残す。</summary>
    public static void Trim(int max = MaxUnmarkedRecords)
    {
        lock (Gate) TrimWithinLock(max);
    }

    private static void TrimWithinLock(int max)
    {
        //ファイル名が開始時刻で始まるため、名前順＝古い順になる。
        var files = EnumerateRecordFiles(RootPath)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        for (int i = 0; i < files.Length - max; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch (Exception e)
            {
                Logger.Warning($"Failed to delete an old game record. ({Path.GetFileName(files[i])})\n" + e.Message);
            }
        }
    }

    // ------------------------------------------------------------------ 小物

    /// <summary>
    /// フォルダ直下の記録ファイルだけを列挙する。<c>Marked</c> は下位フォルダなので自然に外れる。
    /// </summary>
    private static string[] EnumerateRecordFiles(string directory)
    {
        if (!Directory.Exists(directory)) return [];

        try
        {
            return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e)
        {
            Logger.Warning($"Failed to enumerate game records. ({directory})\n" + e.Message);
            return [];
        }
    }

    private static string PathOf(string directory, string id) =>
        directory + Path.DirectorySeparatorChar + id + ".json";

    private static string? LocateWithinLock(string id)
    {
        var path = PathOf(RootPath, id);
        if (File.Exists(path)) return path;

        path = PathOf(MarkedPath, id);
        if (File.Exists(path)) return path;

        return null;
    }

    /// <summary>
    /// 名前順で並べると時刻順になる識別子を作る。同じ秒に重なっても衝突しないよう後ろに乱数を足す。
    /// </summary>
    private static string GenerateId(DateTime timeUtc)
    {
        var stamp = timeUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        return stamp + "_" + suffix;
    }
}

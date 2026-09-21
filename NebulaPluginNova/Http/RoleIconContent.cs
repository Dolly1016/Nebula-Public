using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;
using Nebula.Roles;
using UnityEngine;
using Virial;

namespace Nebula.Http;

/// <summary>
/// 役職アイコンのアトラスと、その対応表を閲覧画面へ渡す。
/// </summary>
/// <remarks>
/// <para>
/// アトラスは <see cref="RoleIcon.RuntimeSpriteGenerator"/> が起動時に作る実行時のテクスチャで、
/// 入れているアドオンによって中身が変わる。そのため PNG をリポジトリに焼いておくことはできず、
/// その場でエンコードして配る。
/// </para>
/// <para>
/// <c>Texture2D.EncodeToPNG</c> は Unity のメインスレッドからしか呼べないが、HTTP は別スレッドで動く。
/// そこで <see cref="Prepare"/> をサーバー起動時（メインスレッド）に一度だけ呼んでバイト列にしておき、
/// HTTP 側はその控えを返すだけにしている。ディスパッチャを増やさずに済ませるための作りである。
/// </para>
/// </remarks>
internal static class RoleIconContent
{
    private static readonly object Gate = new();

    /// <summary>シート名 → PNG。<see cref="Prepare"/> が埋める。</summary>
    private static readonly Dictionary<string, byte[]> sheets = new(StringComparer.Ordinal);

    private static readonly Virial.Logging.ILogger Logger = NebulaAPI.Logging.NebulaLogger("HttpServer");

    public const string PngContentType = "image/png";

    /// <summary>
    /// アトラスを PNG にして控えておく。<b>メインスレッドから呼ぶこと。</b>
    /// </summary>
    /// <remarks>役職の顔ぶれは起動中に変わらないので、二度目以降は何もしない。</remarks>
    public static void Prepare()
    {
        lock (Gate)
        {
            if (sheets.Count > 0) return;

            var names = RoleIcon.RuntimeSpriteGenerator.SheetNames;
            var atlases = RoleIcon.RuntimeSpriteGenerator.Atlases;

            for (int i = 0; i < names.Count && i < atlases.Count; i++)
            {
                try
                {
                    var png = ImageConversion.EncodeToPNG(atlases[i]);
                    if (png != null && png.Length > 0) sheets[names[i]] = png;
                }
                catch (Exception e)
                {
                    Logger.Warning($"Failed to encode a role icon atlas. ({names[i]})\n" + e.Message);
                }
            }

            Logger.Message($"Prepared {sheets.Count} role icon sheet(s).");
        }
    }

    /// <summary>シート 1 枚の PNG。用意できていないか、そんな名前が無ければ false。</summary>
    public static bool TryGetSheet(string name, out byte[] png)
    {
        lock (Gate) return sheets.TryGetValue(name, out png!);
    }

    /// <summary>
    /// アイコンの対応表。
    /// </summary>
    /// <remarks>
    /// 役職名は引く時点の言語で入れるので、控えずに毎回組み立てる。
    /// 対応表そのものは起動中に変わらないが、言語は途中で変わりうるため。
    /// </remarks>
    public static string Manifest
    {
        get
        {
            List<string> names;
            lock (Gate) names = [.. sheets.Keys];

            //控えた順ではなくシートの並び順で返す。閲覧画面は番号で引く。
            var ordered = RoleIcon.RuntimeSpriteGenerator.SheetNames.Where(names.Contains).ToList();
            return BuildManifest(RoleIcon.RuntimeSpriteGenerator.SheetNames, ordered);
        }
    }

    private static string BuildManifest(IReadOnlyList<string> allNames, IReadOnlyList<string> available)
    {
        var view = new RoleIconManifestView
        {
            Cell = RoleIcon.RuntimeSpriteGenerator.IconCellSize,
            Columns = RoleIcon.RuntimeSpriteGenerator.IconColumns,
            Rows = RoleIcon.RuntimeSpriteGenerator.IconRows,
            Sheets = [.. allNames],
        };

        foreach (var entry in RoleIcon.RuntimeSpriteGenerator.IconMap)
        {
            //PNGを用意できなかったシートのアイコンは、出しても描けないので載せない。
            if (entry.Value.sheet >= allNames.Count || !available.Contains(allNames[entry.Value.sheet])) continue;

            //[シート番号, マス番号] の形で持つ。項目数が多いので、名前付きの入れ物にはしない。
            view.Icons[entry.Key] = [entry.Value.sheet, entry.Value.cell];
            view.Names[entry.Key] = entry.Value.assignable.DisplayName;
        }

        return JsonSerializer.Serialize(view, SerializerOptions);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class RoleIconManifestView
    {
        /// <summary>1 マスの大きさ（ピクセル）。</summary>
        public int Cell { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }

        /// <summary>シート名。<c>/role-icons/{名前}.png</c> で引ける。</summary>
        public string[] Sheets { get; set; } = [];

        /// <summary>スプライトタグの名前 → [シート番号, マス番号]。</summary>
        public Dictionary<string, int[]> Icons { get; set; } = [];

        /// <summary>スプライトタグの名前 → 役職などの表示名。閲覧時の言語。</summary>
        public Dictionary<string, string> Names { get; set; } = [];
    }
}

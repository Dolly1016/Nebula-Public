using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Nebula.Game.Statistics;

/// <summary>
/// 嵩む項目を、JSON にしてから圧縮し、1 個の文字列に収めるための道具。
/// </summary>
/// <remarks>
/// <para>
/// 圧縮は GZip。Base64 は圧縮ではなく、バイト列を JSON の文字列に載せるための符号化で、
/// それ自体はむしろ 4/3 倍に嵩む。圧縮したうえで包むことで初めて小さくなる。
/// </para>
/// <para>
/// GZip を選んだのは、閲覧画面がブラウザの <c>DecompressionStream("gzip")</c> でそのまま解けるため。
/// 圧縮したまま配れば、通信量も小さいまま済む。
/// </para>
/// </remarks>
internal static class CompressedJson
{
    /// <summary>
    /// JSON にして圧縮し、Base64 の文字列にする。中身が空なら null。
    /// </summary>
    public static string? Compress<T>(T value, JsonSerializerOptions options)
    {
        if (value == null) return null;

        var json = JsonSerializer.SerializeToUtf8Bytes(value, options);
        if (json.Length == 0) return null;

        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(memory, System.IO.Compression.CompressionLevel.SmallestSize, true))
            gzip.Write(json, 0, json.Length);

        return Convert.ToBase64String(memory.ToArray());
    }

    /// <summary>
    /// <see cref="Compress"/> で作った文字列を元に戻す。読めなければ default。
    /// </summary>
    public static T? Decompress<T>(string? text, JsonSerializerOptions options)
    {
        if (string.IsNullOrEmpty(text)) return default;

        try
        {
            using var source = new MemoryStream(Convert.FromBase64String(text));
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var json = new MemoryStream();
            gzip.CopyTo(json);

            return JsonSerializer.Deserialize<T>(json.ToArray(), options);
        }
        catch (Exception)
        {
            //壊れた記録でも、他の項目は読めるようにしておく。
            return default;
        }
    }
}

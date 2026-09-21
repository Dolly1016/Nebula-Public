using System;
using System.Collections.Generic;
using System.IO;
using Nebula.Modules;
using Virial;

namespace Nebula.Http;

/// <summary>
/// 閲覧画面の HTML/CSS/JS を DLL の埋め込みリソースから取り出す。
/// </summary>
/// <remarks>
/// 実体は <c>NebulaPluginNova/Resources/Http/</c> 配下。
/// csproj の <c>EmbeddedResource Include="Resources\*\*.*"</c> が拾うのは 1 階層だけなので、
/// このフォルダにサブフォルダを作ってはいけない。
/// 取り出しには既存の <see cref="NebulaResourceManager"/> をそのまま使う。
/// </remarks>
internal static class EmbeddedWebContent
{
    /// <summary>公開するファイルと、その Content-Type。ここに無いものは配信しない。</summary>
    private static readonly Dictionary<string, string> Served = new(StringComparer.OrdinalIgnoreCase)
    {
        { "index.html", "text/html; charset=utf-8" },
        { "app.js", "text/javascript; charset=utf-8" },
        { "player-icon.js", "text/javascript; charset=utf-8" },
        { "map-image.js", "text/javascript; charset=utf-8" },
        { "style.css", "text/css; charset=utf-8" },
        { "here_icon.png", "image/png" },
    };

    /// <summary>
    /// 文言の訳を持っている言語。<c>Language.GetLanguage</c> が返す名前で書く。
    /// ここに無い言語は英語のまま表示される。
    /// </summary>
    /// <remarks>訳を足すときは <c>Resources/Http/text.&lt;言語名&gt;.css</c> を作り、ここへ名前を加える。</remarks>
    private static readonly string[] TranslatedLanguages = ["English", "Japanese"];

    /// <summary>文言ファイルの Content-Type。</summary>
    public const string TextContentType = "text/css; charset=utf-8";

    /// <summary>土台として常に読み込ませる文言ファイル。</summary>
    public const string EnglishTextFile = "text.English.css";

    private static readonly Virial.Logging.ILogger Logger =
        NebulaAPI.Logging.NebulaLogger("HttpServer");

    public static bool Exists(string fileName) => Served.ContainsKey(fileName);

    /// <summary>その言語の文言ファイルの名前。訳を持っていない言語なら null。</summary>
    public static string? TextFileOf(string language)
    {
        foreach (var known in TranslatedLanguages)
            if (string.Equals(known, language, StringComparison.OrdinalIgnoreCase)) return "text." + known + ".css";
        return null;
    }

    /// <summary>
    /// 埋め込みリソースを名前で読む。公開して良いかは見ないので、呼び出し側が名前を決めきっていること。
    /// </summary>
    public static bool TryRead(string fileName, out byte[] content)
    {
        content = [];

        try
        {
            using var stream = NebulaResourceManager.GetResource("Nebula::Http." + fileName)?.AsStream();
            if (stream == null)
            {
                Logger.Warning($"Missing an embedded web resource. ({fileName})");
                return false;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            content = memory.ToArray();
            return true;
        }
        catch (Exception e)
        {
            Logger.Warning($"Failed to read an embedded web resource. ({fileName})\n" + e.Message);
            return false;
        }
    }

    /// <summary>
    /// ファイル 1 個を取り出す。公開対象でないか、読めなければ false。
    /// </summary>
    public static bool TryGet(string fileName, out byte[] content, out string contentType)
    {
        content = [];
        contentType = "application/octet-stream";

        if (!Served.TryGetValue(fileName, out var type)) return false;
        contentType = type;

        return TryRead(fileName, out content);
    }
}

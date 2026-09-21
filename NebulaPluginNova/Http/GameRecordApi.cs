using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nebula.Game.Statistics;

namespace Nebula.Http;

/// <summary>
/// <see cref="NebulaHttpServer"/> が受けたリクエストの振り分け。
/// </summary>
/// <remarks>
/// <code>
/// GET  /                       閲覧画面
/// GET  /app.js /style.css      その付属物
/// GET  /text.english.css       文言の土台。常に英語
/// GET  /text.current.css       使用中の言語の文言。訳が無ければ空
/// GET  /api/role-icons         役職アイコンの対応表
/// GET  /role-icons/{名前}.png  役職アイコンのアトラス
/// GET  /api/maps               ミニマップの一覧と座標の変換に要る値
/// GET  /maps/{マップID}.png    ミニマップの画像
/// GET  /api/games              記録の一覧。新しい順
/// GET  /api/games/{id}         記録 1 件。保存されている JSON をそのまま
/// POST /api/games/{id}/mark    マークの付け外し。ボディは {"marked": true}
/// </code>
/// </remarks>
internal static class GameRecordApi
{
    private const string ApiPrefix = "/api/games";

    //文言の配信口。実体のファイル名は言語ごとに違うので、この 2 つだけを外へ見せる。
    private const string EnglishTextPath = "/text.english.css";
    private const string CurrentTextPath = "/text.current.css";

    //役職アイコン。実体は埋め込みリソースではなく実行時に作るテクスチャなので、静的配信とは別扱い。
    private const string RoleIconApiPath = "/api/role-icons";
    private const string RoleIconPrefix = "/role-icons/";

    //ミニマップ。こちらも実行時に描き起こすので静的配信とは別扱い。
    private const string MapApiPath = "/api/maps";
    private const string MapPrefix = "/maps/";

    public static void Handle(HttpExchange exchange)
    {
        var path = Normalize(exchange.Path);

        if (path.StartsWith(ApiPrefix, StringComparison.Ordinal))
        {
            HandleApi(exchange, path);
            return;
        }

        if (path == EnglishTextPath || path == CurrentTextPath)
        {
            HandleText(exchange, path == CurrentTextPath);
            return;
        }

        if (path == RoleIconApiPath)
        {
            if (exchange.Method != "GET")
            {
                exchange.RespondStatus(405);
                return;
            }

            exchange.RespondJson(RoleIconContent.Manifest);
            return;
        }

        if (path.StartsWith(RoleIconPrefix, StringComparison.Ordinal))
        {
            HandleRoleIconSheet(exchange, path.Substring(RoleIconPrefix.Length));
            return;
        }

        if (path == MapApiPath)
        {
            if (exchange.Method != "GET")
            {
                exchange.RespondStatus(405);
                return;
            }

            exchange.RespondJson(MapContent.Manifest);
            return;
        }

        if (path.StartsWith(MapPrefix, StringComparison.Ordinal))
        {
            HandleMapImage(exchange, path.Substring(MapPrefix.Length));
            return;
        }

        HandleStatic(exchange, path);
    }

    // ------------------------------------------------------------------ 役職アイコン

    private static void HandleRoleIconSheet(HttpExchange exchange, string fileName)
    {
        if (exchange.Method != "GET")
        {
            exchange.RespondStatus(405);
            return;
        }

        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            exchange.RespondStatus(404);
            return;
        }

        //名前はファイルパスには使わない。控えてあるシートに一致するものだけを返す。
        if (!RoleIconContent.TryGetSheet(fileName.Substring(0, fileName.Length - 4), out var png))
        {
            exchange.RespondStatus(404);
            return;
        }

        exchange.Respond(200, RoleIconContent.PngContentType, png);
    }

    // ------------------------------------------------------------------ ミニマップ

    private static void HandleMapImage(HttpExchange exchange, string fileName)
    {
        if (exchange.Method != "GET")
        {
            exchange.RespondStatus(405);
            return;
        }

        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || !byte.TryParse(fileName.Substring(0, fileName.Length - 4), out var mapId)
            || !MapContent.TryGetImage(mapId, out var png))
        {
            exchange.RespondStatus(404);
            return;
        }

        exchange.Respond(200, MapContent.PngContentType, png);
    }

    // ------------------------------------------------------------------ 文言

    /// <summary>
    /// 文言の CSS を返す。英語を土台に敷き、その上に使用中の言語を重ねる前提。
    /// </summary>
    /// <remarks>
    /// 訳を持っていない言語では空の CSS を返す。重ねるものが無ければ土台の英語がそのまま残るので、
    /// フォールバックはカスケード任せでよい。
    /// </remarks>
    private static void HandleText(HttpExchange exchange, bool current)
    {
        if (exchange.Method != "GET")
        {
            exchange.RespondStatus(405);
            return;
        }

        var fileName = current
            ? EmbeddedWebContent.TextFileOf(Nebula.Modules.Language.GetCurrentLanguage())
            : EmbeddedWebContent.EnglishTextFile;

        if (fileName == null || !EmbeddedWebContent.TryRead(fileName, out var content))
        {
            exchange.Respond(200, EmbeddedWebContent.TextContentType, []);
            return;
        }

        exchange.Respond(200, EmbeddedWebContent.TextContentType, content);
    }

    // ------------------------------------------------------------------ 静的配信

    private static void HandleStatic(HttpExchange exchange, string path)
    {
        if (exchange.Method != "GET")
        {
            exchange.RespondStatus(405);
            return;
        }

        var fileName = path == "/" ? "index.html" : path.Substring(1);

        //埋め込みリソースは平らに置いてあるので、階層を含む要求は受け付けない。
        if (fileName.Contains('/') || !EmbeddedWebContent.TryGet(fileName, out var content, out var contentType))
        {
            exchange.RespondStatus(404);
            return;
        }

        exchange.Respond(200, contentType, content);
    }

    // ------------------------------------------------------------------ API

    private static void HandleApi(HttpExchange exchange, string path)
    {
        //  /api/games            → ""
        //  /api/games/{id}       → "/{id}"
        //  /api/games/{id}/mark  → "/{id}/mark"
        var rest = path.Substring(ApiPrefix.Length);

        if (rest.Length == 0)
        {
            if (exchange.Method != "GET")
            {
                exchange.RespondStatus(405);
                return;
            }

            exchange.RespondJson(GameRecordView.SerializeList(GameRecordStore.List()));
            return;
        }

        if (rest[0] != '/')
        {
            exchange.RespondStatus(404);
            return;
        }

        var segments = rest.Substring(1).Split('/');

        if (segments.Length == 1)
        {
            HandleSingle(exchange, segments[0]);
            return;
        }

        if (segments.Length == 2 && segments[1] == "mark")
        {
            HandleMark(exchange, segments[0]);
            return;
        }

        exchange.RespondStatus(404);
    }

    private static void HandleSingle(HttpExchange exchange, string id)
    {
        if (exchange.Method != "GET")
        {
            exchange.RespondStatus(405);
            return;
        }

        var stored = GameRecordStore.Read(id);
        if (stored == null)
        {
            exchange.RespondStatus(404);
            return;
        }

        exchange.RespondJson(GameRecordView.Serialize(stored));
    }

    private static void HandleMark(HttpExchange exchange, string id)
    {
        if (exchange.Method != "POST")
        {
            exchange.RespondStatus(405);
            return;
        }

        MarkRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<MarkRequest>(exchange.BodyAsText);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request == null)
        {
            exchange.RespondJson(Error("The request body must be {\"marked\": true} or {\"marked\": false}."), 400);
            return;
        }

        if (!GameRecordStore.SetMarked(id, request.Marked))
        {
            exchange.RespondJson(Error("No such game record."), 404);
            return;
        }

        exchange.RespondJson(JsonSerializer.Serialize(new MarkResponse { Id = id, Marked = request.Marked }));
    }

    // ------------------------------------------------------------------ 小物

    /// <summary>末尾の余分なスラッシュを落とす。<c>/</c> 自身は残す。</summary>
    private static string Normalize(string path)
    {
        if (path.Length == 0) return "/";
        if (path[0] != '/') path = "/" + path;
        while (path.Length > 1 && path.EndsWith('/')) path = path.Substring(0, path.Length - 1);
        return path;
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new ErrorResponse { Message = message });

    private sealed class MarkRequest
    {
        [JsonPropertyName("marked")] public bool Marked { get; set; }
    }

    private sealed class MarkResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("marked")] public bool Marked { get; set; }
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }
}

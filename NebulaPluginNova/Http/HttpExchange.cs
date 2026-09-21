using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Nebula.Http;

/// <summary>
/// 受け取ったリクエスト 1 本と、それに対する応答。
/// </summary>
/// <remarks>
/// 扱うのは同じ PC のブラウザからの単純なリクエストだけなので、
/// チャンク転送・キープアライブ・パイプラインは一切扱わない。
/// 応答には必ず <c>Connection: close</c> を付け、1 リクエスト 1 接続で閉じる。
/// </remarks>
internal sealed class HttpExchange
{
    /// <summary>ボディとして受け入れる最大の大きさ。</summary>
    private const int MaxBodyLength = 1 << 20;

    /// <summary>リクエスト行とヘッダを合わせた最大の大きさ。</summary>
    private const int MaxHeaderLength = 16 * 1024;

    public string Method { get; private init; } = "GET";

    /// <summary>クエリを除いたパス。先頭の <c>/</c> を含み、パーセントデコード済み。</summary>
    public string Path { get; private init; } = "/";

    /// <summary>クエリ文字列。<c>?</c> は含まない。無ければ空文字。</summary>
    public string Query { get; private init; } = "";

    public IReadOnlyDictionary<string, string> Headers { get; private init; } =
        new Dictionary<string, string>();

    public byte[] Body { get; private init; } = [];

    public string BodyAsText => Encoding.UTF8.GetString(Body);

    private readonly Stream stream;

    private HttpExchange(Stream stream)
    {
        this.stream = stream;
    }

    // ------------------------------------------------------------------ 読み取り

    /// <summary>
    /// リクエストを 1 本読む。形が壊れていれば null。
    /// </summary>
    public static HttpExchange? Read(Stream stream)
    {
        var head = ReadHead(stream);
        if (head == null) return null;

        var lines = head.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        //リクエスト行: METHOD SP TARGET SP HTTP/1.1
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2) return null;

        var target = requestLine[1];
        var queryAt = target.IndexOf('?');
        var rawPath = queryAt < 0 ? target : target.Substring(0, queryAt);
        var query = queryAt < 0 ? "" : target.Substring(queryAt + 1);

        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon <= 0) continue;
            headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
        }

        byte[] body = [];
        if (headers.TryGetValue("Content-Length", out var lengthText)
            && int.TryParse(lengthText, out var length)
            && length > 0)
        {
            if (length > MaxBodyLength) return null;
            body = ReadExactly(stream, length);
            if (body.Length != length) return null;
        }

        return new HttpExchange(stream)
        {
            Method = requestLine[0].ToUpperInvariant(),
            Path = Uri.UnescapeDataString(rawPath),
            Query = query,
            Headers = headers,
            Body = body,
        };
    }

    /// <summary>空行が現れるまでを 1 バイトずつ読む。ヘッダはどのみち短い。</summary>
    private static string? ReadHead(Stream stream)
    {
        var buffer = new List<byte>(1024);
        int matched = 0;

        while (matched < 4)
        {
            int read;
            try
            {
                read = stream.ReadByte();
            }
            catch (IOException)
            {
                return null;
            }

            if (read < 0) return null;
            if (buffer.Count >= MaxHeaderLength) return null;

            buffer.Add((byte)read);

            //"\r\n\r\n" の検出
            matched = read switch
            {
                '\r' => matched == 2 ? 3 : 1,
                '\n' => matched is 1 or 3 ? matched + 1 : 0,
                _ => 0,
            };
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static byte[] ReadExactly(Stream stream, int length)
    {
        var buffer = new byte[length];
        int filled = 0;

        while (filled < length)
        {
            int read;
            try
            {
                read = stream.Read(buffer, filled, length - filled);
            }
            catch (IOException)
            {
                break;
            }

            if (read <= 0) break;
            filled += read;
        }

        return filled == length ? buffer : buffer[..filled];
    }

    // ------------------------------------------------------------------ 応答

    public void RespondJson(string json, int status = 200) =>
        Respond(status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));

    public void RespondText(string text, int status = 200) =>
        Respond(status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text));

    /// <summary>本文を持たせず状態だけ返す。</summary>
    public void RespondStatus(int status) =>
        RespondText($"{status} {ReasonPhrase(status)}", status);

    public void Respond(int status, string contentType, byte[] content)
    {
        var header = new StringBuilder()
            .Append("HTTP/1.1 ").Append(status).Append(' ').Append(ReasonPhrase(status)).Append("\r\n")
            .Append("Content-Type: ").Append(contentType).Append("\r\n")
            .Append("Content-Length: ").Append(content.Length).Append("\r\n")
            //ローカル閲覧用の使い捨てサーバーなので、内容を一切キャッシュさせない。
            .Append("Cache-Control: no-store\r\n")
            .Append("X-Content-Type-Options: nosniff\r\n")
            .Append("Connection: close\r\n")
            .Append("\r\n")
            .ToString();

        try
        {
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(content, 0, content.Length);
            stream.Flush();
        }
        catch (IOException)
        {
            //相手が先に切っただけ。放っておく。
        }
        catch (SocketException)
        {
        }
    }

    private static string ReasonPhrase(int status) => status switch
    {
        200 => "OK",
        204 => "No Content",
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        413 => "Payload Too Large",
        500 => "Internal Server Error",
        _ => "Unknown",
    };
}

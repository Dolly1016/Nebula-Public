using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nebula.Online;

internal sealed class NoSResult
{
    [JsonPropertyName("resultId")] public int ResultId { get; set; } = int.MinValue;
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("removed")] public bool Removed { get; set; }

    public bool Ok => ResultId == NoSApi.ResultOk;
}

internal sealed class UidRequest
{
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
}

internal sealed class UidAndNameRequest
{
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
    /// <summary>そのときに名乗る自分のプレイヤー名。相手の一覧に参考として残る。</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

/// <summary>
/// NoS サーバーとの署名付きやりとりをまとめた入口。
/// </summary>
/// <remarks>
/// すべて uid と秘密鍵で署名する。鍵を持っていない、あるいは登録が済んでいない場合は
/// 何も送らずに失敗として返す。呼び出し側はコルーチンとして回すこと。
/// </remarks>
internal static class NoSApi
{
    public const int ResultOk = 0;
    public const int ResultInvalidRequest = -1;
    public const int ResultAuthFailed = -2;
    public const int ResultBanned = -4;
    public const int ResultConflict = -5;
    public const int ResultNotFound = -6;

    /// <summary>登録時にサーバーへ伝えるクライアントの種別。</summary>
    private const string ClientVersion = "NoS";

    // ---------------------------------------------------------------- 土台

    /// <summary>
    /// 署名付きの POST を 1 回行う。応答を <typeparamref name="TResponse"/> として読む。
    /// </summary>
    public static IEnumerator CoPost<TResponse>(
        string method, object? request, Action<TResponse> onSuccess, Action? onFailed = null)
        where TResponse : class
    {
        var identity = NoSIdentity.Instance;
        if (identity == null)
        {
            onFailed?.Invoke();
            yield break;
        }

        yield return CoPost(identity, method, request, onSuccess, onFailed);
    }

    /// <summary>身元を明示して署名付きの POST を行う。登録前の呼び出しはこちらを使う。</summary>
    public static IEnumerator CoPost<TResponse>(
        NoSIdentity identity, string method, object? request, Action<TResponse> onSuccess, Action? onFailed = null)
        where TResponse : class
    {
        var json = request == null ? "{}" : JsonSerializer.Serialize(request, request.GetType());
        var headers = identity.BuildHeaders(method, Encoding.UTF8.GetBytes(json));

        yield return NebulaWebRequest.CoPost(NebulaWebRequest.GetNoSAPI(method), json, true,
            text =>
            {
                try
                {
                    var response = JsonSerializer.Deserialize<TResponse>(text);
                    if (response != null) onSuccess.Invoke(response);
                    else onFailed?.Invoke();
                }
                catch (Exception e)
                {
                    LogUtils.WriteToConsole($"NoS: failed to parse the response of {method}. {e.Message}");
                    onFailed?.Invoke();
                }
            },
            onFailed,
            webRequest =>
            {
                foreach (var header in headers) webRequest.SetRequestHeader(header.Key, header.Value);
            });
    }

    /// <summary>
    /// 登録を確実にする。未登録なら uid を取りに行き、登録済みなら何もしない。
    /// </summary>
    public static IEnumerator CoEnsureRegistered(Action<bool> onDone)
    {
        var identity = NoSIdentity.Instance;
        if (identity == null)
        {
            onDone.Invoke(false);
            yield break;
        }

        if (identity.IsRegistered)
        {
            onDone.Invoke(true);
            yield break;
        }

        var registered = false;
        yield return CoPost<IdentityRegisterResponse>(identity, "identity/register",
            new IdentityRegisterRequest { ClientVersion = ClientVersion },
            response =>
            {
                if (response.ResultId == ResultOk && !string.IsNullOrEmpty(response.Uid))
                {
                    identity.SetUid(response.Uid);
                    registered = true;
                }
                else
                {
                    LogUtils.WriteToConsole($"NoS: identity/register returned resultId={response.ResultId}.");
                }
            });

        onDone.Invoke(registered);
    }

    // ---------------------------------------------------------------- 一括取得

    /// <summary>
    /// ブロック・フレンド・受け取った申請をまとめて取り寄せ、手元の写しを入れ替える。
    /// </summary>
    public static IEnumerator CoLoadSocial(Action<bool>? onDone = null)
    {
        var ready = false;
        yield return CoEnsureRegistered(ok => ready = ok);
        if (!ready)
        {
            onDone?.Invoke(false);
            yield break;
        }

        var loaded = false;
        yield return CoPost<SocialResponse>("players/social", null,
            response =>
            {
                if (response.ResultId != ResultOk)
                {
                    LogUtils.WriteToConsole($"NoS: players/social returned resultId={response.ResultId}.");
                    return;
                }
                NoSSocial.Load(response);
                loaded = true;
            });

        onDone?.Invoke(loaded);
    }

    // ---------------------------------------------------------------- ブロック

    /// <summary>ブロックする。成功したら手元の写しからフレンドと申請も外れる。</summary>
    public static IEnumerator CoBlock(string uid, Action<NoSResult>? onDone = null)
    {
        yield return CoSend("players/block", new UidRequest { Uid = uid }, onDone,
            _ => NoSSocial.ApplyBlocked(uid));
    }

    public static IEnumerator CoUnblock(string uid, Action<NoSResult>? onDone = null)
    {
        yield return CoSend("players/unblock", new UidRequest { Uid = uid }, onDone,
            _ => NoSSocial.ApplyUnblocked(uid));
    }

    // ---------------------------------------------------------------- フレンド

    /// <summary>フレンド申請を出す。相手から先に申請が来ていれば、その場でフレンドになる。</summary>
    public static IEnumerator CoSendFriendRequest(string uid, string myName, Action<NoSResult>? onDone = null)
    {
        yield return CoSend("players/friend/request", new UidAndNameRequest { Uid = uid, Name = myName }, onDone,
            _ => NoSSocial.ApplyRequestSent(uid));
    }

    /// <summary>受け取った申請を承認する。</summary>
    public static IEnumerator CoAcceptFriend(string uid, string myName, Action<NoSResult>? onDone = null)
    {
        yield return CoSend("players/friend/accept", new UidAndNameRequest { Uid = uid, Name = myName }, onDone,
            result => NoSSocial.ApplyAccepted(uid, result.Name));
    }

    /// <summary>受け取った申請を断る。</summary>
    public static IEnumerator CoDenyFriend(string uid, Action<NoSResult>? onDone = null)
    {
        yield return CoSend("players/friend/deny", new UidRequest { Uid = uid }, onDone,
            _ => NoSSocial.ApplyDenied(uid));
    }

    /// <summary>フレンドを解除する。関係は相互なので相手側からも消える。</summary>
    public static IEnumerator CoRemoveFriend(string uid, Action<NoSResult>? onDone = null)
    {
        yield return CoSend("players/friend/remove", new UidRequest { Uid = uid }, onDone,
            _ => NoSSocial.ApplyFriendRemoved(uid));
    }

    // ---------------------------------------------------------------- 共通

    /// <summary>
    /// 1 回の操作を送り、成功したら手元の写しへ同じ変更を入れる。
    /// </summary>
    /// <remarks>
    /// サーバーと同じ規則で手元を書き換えるので、操作のたびに一覧を引き直す必要がない。
    /// </remarks>
    private static IEnumerator CoSend(
        string method, object request, Action<NoSResult>? onDone, Action<NoSResult> apply)
    {
        var result = new NoSResult();

        yield return CoPost<NoSResult>(method, request,
            response =>
            {
                result = response;
                if (response.Ok) apply.Invoke(response);
                else LogUtils.WriteToConsole($"NoS: {method} returned resultId={response.ResultId}.");
            },
            () => LogUtils.WriteToConsole($"NoS: {method} did not reach the server."));

        onDone?.Invoke(result);
    }
}

using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Nebula.Utilities;

internal class NebulaWebRequest
{
    public static IEnumerator CoGet(string url, bool noCache, Action<string> onSuccess, Action? onFailed = null, Action<UnityWebRequest>? modifier = null)
    {
        var request = UnityWebRequest.Get(url);
        if(noCache) request.SetRequestHeader("Cache-Control", "no-cache");
        modifier?.Invoke(request);

        yield return request.SendWebRequest();
        while (request.result == UnityWebRequest.Result.InProgress) yield return null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            LogUtils.WriteToConsole("Failed: " + request.result.ToString());
            onFailed?.Invoke();
            request.Dispose();
            yield break;
        }

        onSuccess(request.downloadHandler.text);
        request.Dispose();
    }

    public static IEnumerator CoGetRaw(string url, bool noCache, Action<byte[]> onSuccess, Action? onFailed = null, Action<UnityWebRequest>? modifier = null)
    {
        var request = UnityWebRequest.Get(url);
        if (noCache) request.SetRequestHeader("Cache-Control", "no-cache");
        modifier?.Invoke(request);

        yield return request.SendWebRequest();
        while (request.result == UnityWebRequest.Result.InProgress) yield return null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            onFailed?.Invoke();
            request.Dispose();
            yield break;
        }

        onSuccess(request.downloadHandler.GetNativeData().ToArray());
        request.Dispose();
    }

    public static IEnumerator CoGetWithParameters(string url, bool noCache, IEnumerable<KeyValuePair<string, string>> parameters, Action<string> onSuccess, Action? onFailed = null, Action<UnityWebRequest>? modifier = null)
    {
        var content = new FormUrlEncodedContent(parameters);
        var stringTask = content.ReadAsStringAsync();
        yield return stringTask.WaitAsCoroutine();
        var rawData = Encoding.UTF8.GetBytes(stringTask.Result);

        var request = new UnityWebRequest(url, "GET");
        if (noCache) request.SetRequestHeader("Cache-Control", "no-cache");
        modifier?.Invoke(request);

        request.uploadHandler = new UploadHandlerRaw(rawData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", content.Headers.ContentType.ToString());

        yield return request.SendWebRequest();
        while (request.result == UnityWebRequest.Result.InProgress) yield return null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            onFailed?.Invoke();
            request.Dispose();
            yield break;
        }

        onSuccess(request.downloadHandler.text);
        request.Dispose();
    }

    public static IEnumerator CoPost(string url, string? json, bool noCache, Action<string> onSuccess, Action? onFailed = null, Action<UnityWebRequest>? modifier = null)
    {
        var request = new UnityWebRequest(url, "POST");
        if (noCache) request.SetRequestHeader("Cache-Control", "no-cache");
        modifier?.Invoke(request);

        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();
        while (request.result == UnityWebRequest.Result.InProgress) yield return null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            onFailed?.Invoke();
            request.Dispose();
            yield break;
        }

        onSuccess(request.downloadHandler.text);
        request.Dispose();
    }
}

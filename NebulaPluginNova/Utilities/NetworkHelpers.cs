using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using Virial.Compat;

namespace Nebula.Utilities;

internal static class RestAPIHelpers
{
    public static IEnumerator CoGetRequest<T>(string url, IEnumerable<KeyValuePair<string, string>> parameters, Wrapping<T> result) where T : class
    {
        if (NebulaPlugin.AllowHttpCommunication)
        {
            yield return NebulaWebRequest.CoGetWithParameters(url, true, parameters, json =>
            {
                result.Value = JsonStructure.Deserialize<T>(json);
            });
        }
        else
        {
            yield break;
        }
    }

#if PC
    async public static Task<HttpStatusCode> PostRequestAsync(string url, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        if (NebulaPlugin.AllowHttpCommunication)
        {
            var content = new StringContent("{" + string.Join(",", parameters.Select(p => "\"" + p.Key + "\":" + p.Value)) + "}", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            var response = await NebulaPlugin.HttpClient.SendAsync(request).ConfigureAwait(false);
            return response.StatusCode;
        }
        else
        {
            return HttpStatusCode.BadRequest;
        }
    }

    async public static Task PostRequestAsync(string url, IEnumerable<KeyValuePair<string, string>> parameters, Action<HttpStatusCode> callback)
    {
        if (NebulaPlugin.AllowHttpCommunication)
        {
            var content = new StringContent("{" + string.Join(",", parameters.Select(p => "\"" + p.Key + "\":" + p.Value)) + "}", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            var response = await NebulaPlugin.HttpClient.SendAsync(request).ConfigureAwait(false);
            callback.Invoke(response.StatusCode);
        }
        else
        {
            return;
        }
    }
#endif
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace Nebula.Utilities;

internal static class RestAPIHelpers
{
    async public static Task<T?> GetRequestAsync<T>(string url, IEnumerable<KeyValuePair<string, string>> parameters) where T : class
    {
        if (NebulaPlugin.AllowHttpCommunication)
        {
            var content = new FormUrlEncodedContent(parameters);
            var request = new HttpRequestMessage(HttpMethod.Get, url){ Content = content };
            var response = await NebulaPlugin.HttpClient.SendAsync(request).ConfigureAwait(false);
            var rawResponse = await response.Content.ReadAsStringAsync();
            return JsonStructure.Deserialize<T>(rawResponse);
        }
        else
        {
            return null;
        }
    }

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
}
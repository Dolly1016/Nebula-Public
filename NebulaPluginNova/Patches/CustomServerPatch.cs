using AmongUs.Data;
using AmongUs.HTTP;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Innersloth.IO;
using Nebula.Behavior;
using Nebula.Modules.Online;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using Virial.Helpers;
using static HttpMatchmakerManager;

namespace Nebula.Patches;

internal static class IRegionInfoExtentions
{
    static public string ModTranslatedName(this IRegionInfo region) => TranslateText(region.Name);
    static public string TranslateText(string text) => Language.TryTranslate("regions." + text, out var translated) ? translated : text;
}

[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.UpdateServerText))]
public static class UpdateGameOptionRegionTextPatch
{
    static void Prefix(CreateGameOptions __instance, [HarmonyArgument(0)] ref string text)
    {
        text = IRegionInfoExtentions.TranslateText(text);
    }
}

[HarmonyPatch(typeof(ServerDropdown), nameof(ServerDropdown.FillServerOptions))]
public static class ServerDropdownPatch
{
    internal static string TranslateText(string text) => Language.TryTranslate("regions." + text, out var translated) ? translated : text;

    const float BaseX = 4.2f;
    const int RegionsPerColumn = 5;
    public static bool Prefix(ServerDropdown __instance)
    {
        var regions = CustomServerLoader.CurrentAvailableRegions().ToArray();
        int num = 0;

        List<ServerListButton> buttons = [];

        foreach (IRegionInfo regionInfo in regions)
        {
            if (DestroyableSingleton<ServerManager>.Instance.CurrentRegion.Name == regionInfo.Name)
            {
                __instance.defaultButtonSelected = __instance.firstOption;
                __instance.firstOption.ChangeButtonText(regionInfo.ModTranslatedName());
            }
            else
            {
                IRegionInfo region = regionInfo;
                ServerListButton serverListButton = __instance.ButtonPool.Get<ServerListButton>();
                serverListButton.transform.localPosition = new Vector3(BaseX * (float)(num / RegionsPerColumn), __instance.y_posButton + -0.55f * (float)(num % RegionsPerColumn), -1f);
                serverListButton.transform.localScale = Vector3.one;
                serverListButton.Text.text = regionInfo.ModTranslatedName();
                serverListButton.Text.ForceMeshUpdate(false, false);
                serverListButton.Button.OnClick.RemoveAllListeners();
                serverListButton.Button.OnClick.AddListener(() => __instance.ChooseOption(region));
                buttons.Add(serverListButton);
                __instance.controllerSelectable.Add(serverListButton.Button);
                num++;
            }
        }

        int columns = (num - 1) / RegionsPerColumn + 1;
        __instance.background.transform.localPosition = new Vector3(0f, __instance.initialYPos + -0.3f * (float)(Math.Min(num, RegionsPerColumn) - 1), 0f);
        __instance.background.size = new Vector2(columns * BaseX, 0.6f + 0.6f * (float)(Math.Min(num, RegionsPerColumn)));
        if(columns > 1)
        {
            foreach (var button in buttons) button.transform.localPosition -= new Vector3((columns - 1) * BaseX * 0.5f, 0f, 0f);
        }


        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoFindGameInfoFromCode))]
public static class FindGameInfoFromCodePatch
{
    public static bool Prefix(AmongUsClient __instance, ref Il2CppSystem.Collections.IEnumerator __result, [HarmonyArgument(0)]int gameId, [HarmonyArgument(1)]Il2CppSystem.Action<HttpMatchmakerManager.FindGameByCodeResponse, string> callback)
    {
        __instance.NetworkMode = NetworkModes.OnlineGame;
        __instance.GameId = gameId;

        __result = ModServerSearcher.CoFindRoomFromCode(HttpMatchmakerManager.Instance, gameId, callback.Invoke).WrapToIl2Cpp();
        return false;
    }
}

file static class ModServerSearcher
{
    static public System.Collections.IEnumerator CoFindRoomFromCode(HttpMatchmakerManager matchmaker, int gameId, Action<FindGameByCodeResponse, string> onFound)
    {
        while (!DestroyableSingleton<EOSManager>.InstanceExists) yield return null;
        yield return DestroyableSingleton<EOSManager>.Instance.WaitForLoginFlow();

        IRegionInfo initialRegion = DestroyableSingleton<ServerManager>.Instance.CurrentRegion;
        List<IRegionInfo> regions = CustomServerLoader.CurrentAvailableRegions().ToList();
        regions.RemoveAll(r => r.Name == initialRegion.Name);
        List<string> errorRegions = [];

        bool found = false;
        yield return CoFindRoomFromCodeOnRegion(matchmaker, initialRegion, gameId, (response, token) =>
        {
            found = true;
            onFound.Invoke(response, token);
        });

        if (found) yield break;

        yield return Effects.All(regions.Select(region => CoFindRoomFromCodeOnRegion(matchmaker, region, gameId, (response, token) =>
        {
            if (found) return;
            found = true;
            DestroyableSingleton<ServerManager>.Instance.SetRegion(region);
            onFound.Invoke(response, token);
        }, (failure) =>
        {
            if (failure.Reason == DisconnectReasons.Custom)
            {
                errorRegions.Add(region.TranslateName == StringNames.NoTranslation ? region.ModTranslatedName() : DestroyableSingleton<TranslationController>.Instance.GetString(region.TranslateName));
            }
        }).WrapToIl2Cpp()).ToArray());

        if (!found)
        {
            var errorTxt = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.ErrorNotFoundGame);
            if (errorRegions.Count > 0)
            {
                errorTxt += "<br>" + Language.Translate("ui.error.failedToSearchRoom") + "<br>";
                errorTxt += Nebula.Utilities.StringHelper.Join(num => num % 3 == 0 ? "<br>" : ",  ", errorRegions);
            }

            HttpMatchmakerManager.Instance.SetDisconnectInfoAndShowPopup(new MatchmakerFailure
            {
                Reason = DisconnectReasons.GameNotFound,
                CustomDisconnect = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.ErrorNotFoundGame),
                MatchmakerError = null,
                ShouldGoOffline = false
            });
            try
            {
                DestroyableSingleton<DisconnectPopup>.Instance.SetText(errorTxt);
            }
            catch { }
        }
    }

    static public System.Collections.IEnumerator CoRefreshToken(HttpMatchmakerManager matchmaker, ServerInfo udpServer, Action<string> onGetToken, Action<MatchmakerFailure>? onFailure = null)
    {
        if (string.IsNullOrEmpty(DestroyableSingleton<EOSManager>.Instance.UserIDToken)) yield break;

        string text = udpServer.HttpUrl + "api/user";
        string text2 = JsonConvert.SerializeObject(new HttpMatchmakerManager.UserTokenRequestData
        {
            Puid = DestroyableSingleton<EOSManager>.Instance.ProductUserId,
            Username = DataManager.Player.Customization.Name,
            ClientVersion = Constants.GetBroadcastVersion(),
            Language = DataManager.Settings.Language.CurrentLanguage
        });
        var retryableWebRequest = RetryableWebRequest.Post(text, text2);
        retryableWebRequest.SetOrReplaceRequestHeader("Content-Type", "application/json");
        retryableWebRequest.SetOrReplaceRequestHeader("Accept", "text/plain");
        retryableWebRequest.SetOrReplaceRequestHeader("Authorization", "Bearer " + DestroyableSingleton<EOSManager>.Instance.UserIDToken);
        retryableWebRequest.SetOrReplaceSuccessCallback((Il2CppSystem.Action<string>)((string encodedToken) =>
        {
            if (!MatchmakerToken.TryParse(encodedToken, out _)) return;
            onGetToken.Invoke(encodedToken);
        }));
        yield return CoSendRequest(matchmaker, udpServer, retryableWebRequest, "authenticate", 2, (failure) =>
        {
            onFailure?.Invoke(failure);
        });
    }

    static public System.Collections.IEnumerator CoFindRoomFromCodeOnRegion(HttpMatchmakerManager matchmaker, IRegionInfo region, int gameId, Action<FindGameByCodeResponse, string> onFound, Action<MatchmakerFailure>? onFailure = null)
    {
        var udpServer = region.Servers.ToArray().Random();
        RetryableWebRequest retryableWebRequest;

        string uri = $"{udpServer.HttpUrl}api/games/{gameId}";
        string matchmakerToken = string.Empty;

        bool error = false;
        yield return CoRefreshToken(matchmaker, udpServer, t => matchmakerToken = t, failure =>
        {
            error = true;
            onFailure?.Invoke(failure);
        }
        );
        if (error) yield break;

        retryableWebRequest = RetryableWebRequest.Get(uri);
        retryableWebRequest.SetOrReplaceRequestHeader("Accept", "application/json");
        retryableWebRequest.SetOrReplaceRequestHeader("Authorization", "Bearer " + matchmakerToken);
        retryableWebRequest.SetOrReplaceSuccessCallback((Il2CppSystem.Action<string>)((string response) =>
        {
            //IL_0074: Expected O, but got Unknown
            try
            {
                FindGameByCodeResponse findGameByCodeResponse = JsonConvert.DeserializeObject<FindGameByCodeResponse>(response, matchmaker.jsonSettings);
                findGameByCodeResponse.Region = region.TranslateName;
                findGameByCodeResponse.UntranslatedRegion = region.ModTranslatedName();
                onFound.Invoke(findGameByCodeResponse, matchmakerToken);
            }
            catch
            {
            }
        }));

        yield return CoSendRequest(matchmaker, udpServer, retryableWebRequest, "request gamecode server", 2, (failure) =>
        {
            onFailure?.Invoke(failure);
        });
    }

    static public System.Collections.IEnumerator CoSendRequest(HttpMatchmakerManager matchmaker, ServerInfo udpServer, RetryableWebRequest request, string context, int maxRetries, Action<HttpMatchmakerManager.MatchmakerFailure> onFailure)
    {
        yield return CoSend(request, matchmaker.logger); //request.CoSend(matchmaker.logger);
        if (request.IsSuccess) yield break;

        if (request.IsAuthError && context == "authenticate")
        {
            onFailure?.Invoke(matchmaker.BundleFailureInfo(request, context));
            yield break;
        }
        if (request.IsAuthError)
        {
            bool didRefreshToken = false;
            yield return CoRefreshToken(matchmaker, udpServer, token =>
            {
                request.SetOrReplaceRequestHeader("Authorization", "Bearer " + token);
                didRefreshToken = true;
            });
            if (!didRefreshToken)
            {
                onFailure?.Invoke(matchmaker.BundleFailureInfo(request, context));
                yield break;
            }
        }
        if (request.IsAuthError || request.IsTransientError)
        {
            float retryIntervalSeconds = 0.2f;
            int retryBackoffFactor = 2;
            for (int retries = 1; retries <= maxRetries; retries++)
            {
                float num = (float)((double)retryIntervalSeconds * Math.Pow(retryBackoffFactor, retries));
                yield return new WaitForSeconds(num);
                yield return CoSend(request, matchmaker.logger); //request.CoSend(matchmaker.logger);
                if (request.IsSuccess)
                {
                    yield break;
                }
                if (!request.IsTransientError)
                {
                    break;
                }
            }
        }
        onFailure?.Invoke(matchmaker.BundleFailureInfo(request, context));
    }

    private static System.Collections.IEnumerator CoSend(RetryableWebRequest retryableRequest, Logger logger)
    {
        UnityWebRequest request = retryableRequest.BuildRequest();
        var operation = request.SendWebRequest();
        while (!operation.isDone) yield return null;

        retryableRequest.ResponseCode = request.responseCode;
        retryableRequest.ResponseText = request.downloadHandler.text;
        retryableRequest.Error = request.error;
        if (retryableRequest.IsSuccess)
        {
            try
            {
                var action = retryableRequest.successCallback;
                action?.Invoke(request.downloadHandler.text);

                yield break;
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error("The success callback passed to RetryableWebRequest threw an exception: " + ex.Message, null);
                }
                retryableRequest.ResponseCode = 0L;
                retryableRequest.ResponseText = null;
                retryableRequest.Error = ex.Message;
            }
        }
        try
        {
            var action2 = retryableRequest.errorCallback;
            action2?.Invoke(retryableRequest);

            yield break;
        }
        catch (Exception ex2)
        {
            if (logger != null)
            {
                logger.Error("The error callback passed to RetryableWebRequest threw an exception: " + ex2.Message, null);
            }
            yield break;
        }
    }
}

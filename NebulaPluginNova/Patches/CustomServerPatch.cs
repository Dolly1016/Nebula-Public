using AmongUs.Data;
using AmongUs.HTTP;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Innersloth.IO;
using Nebula.Behavior;
using Newtonsoft.Json;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using Virial.Helpers;
using static HttpMatchmakerManager;

namespace Nebula.Patches;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[HarmonyPatch(typeof(RegionMenu), nameof(RegionMenu.Open))]
public static class RegionMenuOpenPatch
{
    private static StringDataEntry SaveIp = null!;
    private static IntegerDataEntry SavePort = null!;

    private static TextField? ipField = null;
    private static TextField? portField = null;

    public static IRegionInfo[] defaultRegions = null!;

    private static readonly DataSaver customServerData = new("CustomServer");
    private static StaticHttpRegionInfo /*CustomRegion = null!,*/ NoSServerRegion = null!;
    private const string NoSIP = "https://www.nebula-on-the-ship.com";
    private const ushort NoSPort = 443;

    public static readonly List<IRegionInfo> AddonRegions = [];
    public static IRegionInfo GenerateRegion(string name, string ip, ushort port) => new StaticHttpRegionInfo(name, StringNames.NoTranslation, ip,
            new ServerInfo[] { new ServerInfo("Http-1", ip, port, false) }).Cast<IRegionInfo>();

    private class CustomServerData
    {
        [JsonSerializableField]
        public string DisplayName = null!;
        [JsonSerializableField]
        public string Ip = null!;
        [JsonSerializableField]
        public ushort Port = 443;

        public bool IsValid => DisplayName != null && Ip != null;
    }
    public static void LoadAddonRegion()
    {
        foreach(var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenRead("CustomServer.json");
            if (stream == null) continue;
            var servers = JsonStructure.Deserialize<List<CustomServerData>>(stream);

            foreach(var server in servers ?? [])
            {
                if (server.IsValid)
                {
                    AddonRegions.Add(GenerateRegion(server.DisplayName, server.Ip, server.Port));
                }
            }
        }
    }
    public static void UpdateRegions()
    {
        ServerManager serverManager = DestroyableSingleton<ServerManager>.Instance;
        IRegionInfo[] regions = defaultRegions;

        //var CustomRegion = new DnsRegionInfo(SaveIp.Value, "Custom", StringNames.NoTranslation, SaveIp.Value, (ushort)SavePort.Value, false);
        
        /*
        CustomRegion = new StaticHttpRegionInfo("Custom", StringNames.NoTranslation, SaveIp.Value,
            new ServerInfo[] { new ServerInfo("Custom", SaveIp.Value, (ushort)SavePort.Value, false) });
        */

        NoSServerRegion = new StaticHttpRegionInfo("Nebula on the Ship JP", StringNames.NoTranslation, NoSIP,
            (ServerInfo[])[new("Http-1", NoSIP, NoSPort, false)]);

        var ModNARegion = new StaticHttpRegionInfo("Modded NA (MNA)", StringNames.NoTranslation, "www.aumods.us",
            (ServerInfo[])[new("Http-1", "https://www.aumods.us", 443, false)]);
        var ModEURegion = new StaticHttpRegionInfo("Modded EU (MEU)", StringNames.NoTranslation, "au-eu.duikbo.at",
            (ServerInfo[])[new("Http-1", "https://au-eu.duikbo.at", 443, false)]);
        var ModASRegion = new StaticHttpRegionInfo("Modded Asia (MAS)", StringNames.NoTranslation, "au-as.duikbo.at",
            (ServerInfo[])[new("Http-1", "https://au-as.duikbo.at", 443, false)]);

        regions = regions.Concat(AddonRegions).Concat([ModNARegion.Cast<IRegionInfo>(), ModEURegion.Cast<IRegionInfo>(), ModASRegion.Cast<IRegionInfo>(), NoSServerRegion.Cast<IRegionInfo>()]).ToArray();
        //マージ時、DefaultRegionsに含まれている要素のほうが優先される(重複時に生き残る方)
        ServerManager.DefaultRegions = regions;
        serverManager.LoadServers();
        if(serverManager.CurrentRegion.TranslateName != StringNames.NoTranslation)
        {
            serverManager.StartCoroutine(ManagedEffects.Sequence(ManagedEffects.Wait(3f), ManagedEffects.Action(()=> serverManager.SetRegion(ModASRegion.CastFast<IRegionInfo>()))).WrapToIl2Cpp());
        }
    }

    static RegionMenuOpenPatch()
    {
        LoadAddonRegion();
        defaultRegions = ServerManager.DefaultRegions;
        UpdateRegions();
    }

    private static void ChooseOption(RegionMenu __instance, IRegionInfo region)
    {

        DestroyableSingleton<ServerManager>.Instance.SetRegion(region);
        __instance.RegionText.text = DestroyableSingleton<TranslationController>.Instance.GetStringWithDefault(region.TranslateName, region.Name, new Il2CppReferenceArray<Il2CppSystem.Object>([]));
    }

    /*
    public static void Postfix(RegionMenu __instance)
    {
        if (!__instance.TryCast<RegionMenu>()) return;

        if (!ipField)
        {
            Reference<TextField> ipRef = new();
            var widget = new MetaWidgetOld.TextInput(1, 2f, new(2.8f, 0.3f))
            {
                TextFieldRef = ipRef,
                TextPredicate = (c) => ('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z') || ('0' <= c && c <= '9') || c is '?' or '!' or ',' or '.' or '/' or ':',
                Hint = "Server IP".Color(Color.gray),
                DefaultText = SaveIp.Value
            };
            widget.Generate(__instance.gameObject, new Vector3(0f, -1.2f, -100f),out _);

            ipField = ipRef.Value!;
            ipField.LostFocusAction = (text) =>
            {
                while (text.EndsWith('/')) text = text.Substring(0, text.Length - 1);
                ipField.SetText(text);
                SaveIp.Value = text;
                UpdateRegions();
                ChooseOption(__instance, CustomRegion.Cast<IRegionInfo>());
            };
        }

        if (!portField)
        {
            Reference<TextField> portRef = new();
            var widget = new MetaWidgetOld.TextInput(1, 2f, new(2.8f, 0.3f))
            {
                TextFieldRef = portRef,
                TextPredicate = (c) => '0' <= c && c <= '9',
                Hint = "Server Port".Color(Color.gray),
                DefaultText = SavePort.Value.ToString()
            };
            widget.Generate(__instance.gameObject, new Vector3(0f, -1.8f, -100f), out _);

            portField = portRef.Value!;
            portField.LostFocusAction = (text) =>
            {
                SavePort.Value = ushort.TryParse(text, out var port) ? port : (ushort)22023;
                UpdateRegions();
                ChooseOption(__instance, CustomRegion.Cast<IRegionInfo>());
            };
        }
    }
    */
}

[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.UpdateServerText))]
public static class UpdateGameOptionRegionTextPatch
{
    static void Prefix(CreateGameOptions __instance, [HarmonyArgument(0)] ref string text)
    {
        text = ServerDropdownPatch.TranslateText(text);
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
        var regions = DestroyableSingleton<ServerManager>.Instance.AvailableRegions.Where(r => r.TranslateName == StringNames.NoTranslation).ToArray();
        var defaultRegions = ServerManager.DefaultRegions.ToArray();
        regions = regions.OrderBy(r => defaultRegions.FindIndex(d => d.Name == r.Name)).ToArray();
        int num = 0;

        List<ServerListButton> buttons = [];

        foreach (IRegionInfo regionInfo in regions)
        {
            if (DestroyableSingleton<ServerManager>.Instance.CurrentRegion.Name == regionInfo.Name)
            {
                __instance.defaultButtonSelected = __instance.firstOption;
                __instance.firstOption.ChangeButtonText(TranslateText(regionInfo.Name));
            }
            else
            {
                IRegionInfo region = regionInfo;
                ServerListButton serverListButton = __instance.ButtonPool.Get<ServerListButton>();
                serverListButton.transform.localPosition = new Vector3(BaseX * (float)(num / RegionsPerColumn), __instance.y_posButton + -0.55f * (float)(num % RegionsPerColumn), -1f);
                serverListButton.transform.localScale = Vector3.one;
                serverListButton.Text.text = TranslateText(regionInfo.Name);
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
        List<IRegionInfo> regions = DestroyableSingleton<ServerManager>.Instance.AvailableRegions.ToList();
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
                errorRegions.Add(region.TranslateName == StringNames.NoTranslation ? region.Name.Replace("<br>", "") : DestroyableSingleton<TranslationController>.Instance.GetString(region.TranslateName));
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
                findGameByCodeResponse.UntranslatedRegion = region.Name;
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

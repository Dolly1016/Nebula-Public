using AmongUs.Data;
using AmongUs.Matchmaking;
using InnerNet;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Virial.Text;

namespace Nebula.Patches;

internal static class PublicRoomBrowser
{
    internal const float RefetchIntervalSeconds = 10f;

    internal const string ClientVersionForServer = "NoS";
}

internal sealed class NoSIdentity
{
    private const string SignaturePrefix = "NOSv1";

    private const string AuthSignaturePrefix = "NOSAUTHv1";

    private static readonly DataSaver Saver = new("NoSIdentity");

    private static readonly StringDataEntry StoredUid = new("uid", Saver, "");

    private static readonly StringDataEntry StoredPrivateKey = new("privateKey", Saver, "");

    private readonly ECDsa ecdsa;

    public string PublicKeyBase64 { get; }

    public string UId { get; private set; } = "";

    public bool IsRegistered => !string.IsNullOrEmpty(UId);

    private NoSIdentity(ECDsa ecdsa)
    {
        this.ecdsa = ecdsa;
        PublicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
    }

    private static NoSIdentity? cached = null;

    public static NoSIdentity? Instance
    {
        get
        {
            if (cached != null) return cached;

            try
            {
                if (!string.IsNullOrEmpty(StoredPrivateKey.Value))
                {
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(StoredPrivateKey.Value.Trim()), out _);

                    cached = new NoSIdentity(ecdsa) { UId = StoredUid.Value };
                    return cached;
                }
            }
            catch (Exception e)
            {
                LogUtils.WriteToConsole("NoS: failed to load the stored identity, creating a new one. " + e.Message);
            }

            cached = new NoSIdentity(ECDsa.Create(ECCurve.NamedCurves.nistP256));
            cached.Save();
            return cached;
        }
    }

    public void SetUid(string uid)
    {
        UId = uid ?? "";
        Save();
    }

    private void Save()
    {
        try
        {
            StoredPrivateKey.Value = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
            StoredUid.Value = UId;
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to save the identity. " + e.Message);
        }
    }

    public Dictionary<string, string> BuildHeaders(string method, byte[] body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var bodyDigest = Convert.ToBase64String(SHA256.HashData(body));
        var principal = IsRegistered ? UId : PublicKeyBase64;

        var signingString = string.Join('\n', SignaturePrefix, method, principal, timestamp, nonce, bodyDigest);
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(signingString),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        var headers = new Dictionary<string, string>
        {
            ["X-NoS-Timestamp"] = timestamp,
            ["X-NoS-Nonce"] = nonce,
            ["X-NoS-Signature"] = Convert.ToBase64String(signature),
        };
        if (IsRegistered) headers["X-NoS-Uid"] = UId;
        else headers["X-NoS-Public-Key"] = PublicKeyBase64;

        return headers;
    }

    private static string BuildAuthSigningString(string uid, string gameCode, string issuedAt, byte[] challenge)
    {
        var challengeDigest = Convert.ToBase64String(SHA256.HashData(challenge));
        return string.Join('\n', AuthSignaturePrefix, uid, gameCode, issuedAt, challengeDigest);
    }

    public (string Signature, string IssuedAt)? CreateAuthProof(string gameCode, byte[] challenge)
    {
        if (!IsRegistered) return null;

        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signingString = BuildAuthSigningString(UId, gameCode, issuedAt, challenge);
        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(signingString),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        return (Convert.ToBase64String(signature), issuedAt);
    }
}

internal sealed class PublicRoomEntry
{
    [JsonPropertyName("roomId")] public int RoomId { get; set; }
    [JsonPropertyName("gameId")] public int GameId { get; set; }
    [JsonPropertyName("regionAddr")] public string RegionAddr { get; set; } = "";
    [JsonPropertyName("regionPort")] public int RegionPort { get; set; }
    [JsonPropertyName("serverIp")] public string ServerIp { get; set; } = "";
    [JsonPropertyName("serverPort")] public int ServerPort { get; set; }
    [JsonPropertyName("hostName")] public string HostName { get; set; } = "";
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
    [JsonPropertyName("hostExperience")] public int HostExperience { get; set; } = -1;
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("lang")] public uint Lang { get; set; }
    [JsonPropertyName("map")] public int Map { get; set; }
    [JsonPropertyName("epoch")] public int Epoch { get; set; }
    [JsonPropertyName("buildNum")] public int BuildNum { get; set; }
    [JsonPropertyName("handshakeHash")] public int HandshakeHash { get; set; }
    [JsonPropertyName("tags")] public string[] Tags { get; set; } = [];
    [JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; }
    [JsonPropertyName("numPlayers")] public int NumPlayers { get; set; }
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("elapsed")] public int Elapsed { get; set; }
}

internal sealed class RoomListRequest
{
    [JsonPropertyName("query")] public string Query { get; set; } = "";
    [JsonPropertyName("epoch")] public int Epoch { get; set; }
    [JsonPropertyName("buildNum")] public int BuildNum { get; set; }
    [JsonPropertyName("handshakeHash")] public int HandshakeHash { get; set; }
    [JsonPropertyName("lang")] public int? Lang { get; set; }
    [JsonPropertyName("includeStarted")] public bool IncludeStarted { get; set; } = false;
    [JsonPropertyName("page")] public int Page { get; set; } = 0;
}

internal sealed class RoomListResponse
{
    [JsonPropertyName("resultId")] public int ResultId { get; set; } = int.MinValue;
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("rooms")] public PublicRoomEntry[] Rooms { get; set; } = [];
}

internal sealed class IdentityRegisterRequest
{
    [JsonPropertyName("clientVersion")] public string ClientVersion { get; set; } = "";
}

internal sealed class IdentityRegisterResponse
{
    [JsonPropertyName("resultId")] public int ResultId { get; set; } = int.MinValue;
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
}

internal static class PublicRoomDirectory
{
    private static List<PublicRoomEntry> allRooms = [];

    private static float lastFetchTime = float.NegativeInfinity;
    private static bool fetching = false;

    public static bool ShouldNotRefetchNow => Time.realtimeSinceStartup - lastFetchTime < PublicRoomBrowser.RefetchIntervalSeconds;

    // 将来的にNoSサーバー以外の掲載依頼を受け付けるつもりがあるので、絞り込みは行っておく。
    public static List<PublicRoomEntry> RoomsInCurrentRegion()
    {
        var region = ServerManager.InstanceExists ? ServerManager.Instance.CurrentRegion : null;
        if (region == null) return [];

        HashSet<string> endpoints = [];
        foreach (var server in region.Servers.GetFastEnumerator()) endpoints.Add(server.Ip + ":" + server.Port);
        
        return allRooms.Where(room => endpoints.Contains(room.RegionAddr + ":" + room.RegionPort)).ToList();
    }

    public static void DeleteCache() => lastFetchTime = float.NegativeInfinity;

    // 署名付きPOST
    private static IEnumerator CoPostSigned<TResponse>(
        NoSIdentity identity, string method, object? request, Action<TResponse> onSuccess, Action? onFailed = null)
        where TResponse : class
    {
        string json = request == null ? "{}" : JsonSerializer.Serialize(request, request.GetType());
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

    // uid登録を確実にする。未登録であれば登録し、登録済みなら何もしない。
    private static IEnumerator CoEnsureRegistered(NoSIdentity identity, Action<bool> onDone)
    {
        if (identity.IsRegistered)
        {
            onDone.Invoke(true);
            yield break;
        }

        bool registered = false;
        yield return CoPostSigned<IdentityRegisterResponse>(identity, "identity/register",
            new IdentityRegisterRequest { ClientVersion = PublicRoomBrowser.ClientVersionForServer },
            response =>
            {
                if (response.ResultId == 0 && !string.IsNullOrEmpty(response.Uid))
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

    public static IEnumerator CoFetch(Action<bool> onDone)
    {
        // 既にフェッチ中
        if (fetching)
        {
            onDone.Invoke(false);
            yield break;
        }

        var identity = NoSIdentity.Instance;
        if (identity == null)
        {
            onDone.Invoke(false);
            yield break;
        }

        fetching = true;

        bool ready = false;
        yield return CoEnsureRegistered(identity, ok => ready = ok);

        if (!ready)
        {
            fetching = false;
            onDone.Invoke(false);
            yield break;
        }

        bool succeeded = false;
        // バージョンが 3 つとも一致する部屋だけを受け取る
        yield return CoPostSigned<RoomListResponse>(identity, "rooms/list",
            new RoomListRequest
            {
                IncludeStarted = false,
                Epoch = NebulaPlugin.PluginEpoch,
                BuildNum = NebulaPlugin.PluginBuildNum,
                HandshakeHash = Modules.NebulaAddon.AddonHandshakeHash,
            },
            response =>
            {
                if (response.ResultId != 0) return;
                
                allRooms = (response.Rooms ?? []).ToList();
                lastFetchTime = Time.realtimeSinceStartup;
                succeeded = true;
            });

        fetching = false;
        onDone.Invoke(succeeded);
    }
}

// 以下、Harmonyパッチ

[HarmonyPatch(typeof(HttpMatchmakerManager), nameof(HttpMatchmakerManager.CoRefreshFilters))]
public static class PublicRoomSkipRefreshFiltersPatch
{
    public static bool Prefix(
        ref Il2CppSystem.Collections.IEnumerator __result,
        [HarmonyArgument(0)] Il2CppSystem.Action<PermittedFilters> onRefreshFilters)
    {
        __result = CoSkip(onRefreshFilters).WrapToIl2Cpp();
        return false;
    }

    private static IEnumerator CoSkip(Il2CppSystem.Action<PermittedFilters> onRefreshFilters)
    {
        onRefreshFilters?.Invoke(null);
        yield break;
    }
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.RefreshList))]
public static class PublicRoomRefreshListPatch
{
    public static bool Prefix(FindAGameManager __instance)
    {
        if (__instance.timer < 1f) return false;
        __instance.timer = 0f;

        if (NoSIdentity.Instance == null)
        {
            Render(__instance, []);
            return false;
        }

        if (!DestroyableSingleton<MatchMaker>.Instance.Connecting<FindAGameManager>(__instance)) return false;

        if (PublicRoomDirectory.ShouldNotRefetchNow)
        {
            Render(__instance, PublicRoomDirectory.RoomsInCurrentRegion());
            return false;
        }

        __instance.SetRefresh(false);
        __instance.ResetContainers();
        __instance.StartIcon();

        __instance.StartCoroutine(CoRefresh(__instance).WrapToIl2Cpp());
        return false;
    }

    private static IEnumerator CoRefresh(FindAGameManager manager)
    {
        yield return PublicRoomDirectory.CoFetch(_ => { });

        if (manager == null) yield break;
        Render(manager, PublicRoomDirectory.RoomsInCurrentRegion());
    }

    internal static void Render(FindAGameManager manager, List<PublicRoomEntry> rooms)
    {
        try
        {
            manager.ResetContainers();

            int shown = 0;
            int capacity = manager.gameContainers.Length;
            foreach (var room in rooms)
            {
                if (shown >= capacity) break;

                var container = manager.gameContainers[shown];
                container.gameObject.SetActive(true);
                container.SetGameListing(PublicRoomListingConverter.ToGameListing(room));
                container.SetupGameInfo();
                shown++;
            }

            manager.TotalText.text = rooms.Count.ToString();
            manager.matchesFoundText.text = rooms.Count.ToString();
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to render the public room list. " + e.ToString());
        }
        finally
        {
            manager.StopIcon();
            manager.SetRefresh(true);
            DestroyableSingleton<MatchMaker>.Instance.NotConnecting();
        }
    }
}

internal static class PublicRoomListingConverter
{
    private static readonly Dictionary<int, PublicRoomEntry> byGameId = [];

    public static bool TryGet(int gameId, out PublicRoomEntry room) => byGameId.TryGetValue(gameId, out room!);

    public static GameListing ToGameListing(PublicRoomEntry room)
    {
        // サーバーが数値のまま送ってくるので、文字列コードとの往復は要らない
        int gameId = room.GameId;
        byGameId[gameId] = room;

        return new GameListing
        {
            IP = ToAddress(room.ServerIp),
            Port = (ushort)Mathf.Clamp(room.ServerPort, 0, ushort.MaxValue),
            GameId = gameId,
            HostName = room.HostName,
            TrueHostName = room.HostName,
            HostPlatformName = string.Empty,
            PlayerCount = (byte)Mathf.Clamp(room.NumPlayers, 0, byte.MaxValue),
            MaxPlayers = room.MaxPlayers,
            NumImpostors = 0,
            MapId = (byte)Mathf.Clamp(room.Map, 0, (int)MapNames.Fungle),
            Age = room.Elapsed,
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Platform = Platforms.Unknown,
            QuickChat = QuickChatModes.FreeChatOrQuickChat,
            Options = GameOptionsManager.Instance.GameSearchOptions,
        };
    }

    private static uint ToAddress(string ip)
    {
        try
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) return 0u;
            uint address = 0u;
            for (int i = 0; i < 4; i++) address |= (uint)byte.Parse(parts[i], CultureInfo.InvariantCulture) << (i * 8);
            return address;
        }
        catch
        {
            return 0u;
        }
    }
}

[HarmonyPatch(typeof(GameContainer), nameof(GameContainer.SetupGameInfo))]
public static class PublicRoomGameContainerPatch
{
    public static void Postfix(GameContainer __instance)
    {
        if (!PublicRoomListingConverter.TryGet(__instance.gameListing.GameId, out var room)) return;

        try
        {
            var title = string.IsNullOrEmpty(room.Title) ? room.HostName : room.Title;
            __instance.tag1.text = title;
            __instance.tag2.text = room.HostName;
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to draw a public room entry. " + e.Message);
        }
    }
}

[HarmonyPatch(typeof(FindGameMoreInfoPopup), nameof(FindGameMoreInfoPopup.SetupInfo))]
public static class PublicRoomMoreInfoPatch
{
    public static void Postfix(FindGameMoreInfoPopup __instance, [HarmonyArgument(0)] GameListing gameL)
    {
        if (!PublicRoomListingConverter.TryGet(gameL.GameId, out var room)) return;

        try
        {
            __instance.regionText.text = room.HostName;
            __instance.languageText.text = room.Tags.Length > 0 ? string.Join(", ", room.Tags) : Modules.Language.GetLanguage(room.Lang);
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to draw the public room details. " + e.Message);
        }
    }
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.OnDestroy))]
public static class PublicRoomLeaveBrowserPatch
{
    public static void Postfix() => PublicRoomDirectory.DeleteCache();
}

[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
public static class PublicRoomStartBrowserPatch
{
    public static void Postfix(FindAGameManager __instance)
    {
        var titleAttr = new TextAttribute(Virial.Text.TextAlignment.Left, GUI.API.GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(2.5f, 1.5f, 2.5f), new(3f, 0.5f), new(255, 255, 255), false);
        var hostAttr = new TextAttribute(Virial.Text.TextAlignment.Left, GUI.API.GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.6f, 1.3f, 1.6f), new(3f, 0.3f), new(255, 255, 255), false);

        var titleComponent = GUI.API.RawText(Virial.Media.GUIAlignment.Left, titleAttr, "");
        var hostComponent = GUI.API.RawText(Virial.Media.GUIAlignment.Left, hostAttr, "");

        foreach (var container in __instance.gameContainers.GetFastEnumerator())
        {
            container.tag1.transform.parent.parent.gameObject.SetActive(false);

            var newTag1 = titleComponent.Instantiate(new(100f, 100f), out _);
            var newTag2 = hostComponent.Instantiate(new(100f, 100f), out _);
            
            newTag1.transform.SetParent(container.mapBackground.transform.parent);
            newTag2.transform.SetParent(container.mapBackground.transform.parent);
            newTag1.transform.localPosition = new(-0.6f, 0.1f, -0.1f);
            newTag2.transform.localPosition = new(-0.6f, -0.15f, -0.1f);

            container.tag1 = newTag1.GetComponent<TMPro.TextMeshPro>();
            container.tag2 = newTag2.GetComponent<TMPro.TextMeshPro>();


        }
    }
}




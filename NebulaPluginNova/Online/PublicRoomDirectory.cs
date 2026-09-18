using AmongUs.Data;
using AmongUs.Matchmaking;
using InnerNet;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine.Rendering;
using Virial.Text;

namespace Nebula.Online;

internal class PublicRoomBrief
{
    [JsonPropertyName("regionAddr")] public string RegionAddr { get; set; } = "";
    [JsonPropertyName("regionPort")] public int RegionPort { get; set; }
    [JsonPropertyName("hostName")] public string HostName { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("epoch")] public int Epoch { get; set; }
    [JsonPropertyName("buildNum")] public int BuildNum { get; set; }
    [JsonPropertyName("map")] public int Map { get; set; }
}

internal sealed class PublicRoomEntry : PublicRoomBrief
{
    [JsonPropertyName("roomId")] public int RoomId { get; set; }
    [JsonPropertyName("gameId")] public int GameId { get; set; }
    [JsonPropertyName("serverIp")] public string ServerIp { get; set; } = "";
    [JsonPropertyName("serverPort")] public int ServerPort { get; set; }
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
    [JsonPropertyName("hostExperience")] public int HostExperience { get; set; } = -1;
    [JsonPropertyName("lang")] public uint Lang { get; set; }
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
    [JsonPropertyName("otherAddons")] public int OtherAddons { get; set; }
    [JsonPropertyName("otherAddonRooms")] public PublicRoomBrief[] OtherAddonRooms { get; set; } = [];

    [JsonPropertyName("otherVersions")] public int OtherVersions { get; set; }
    [JsonPropertyName("otherVersionRooms")] public PublicRoomBrief[] OtherVersionRooms { get; set; } = [];
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

    private static List<PublicRoomBrief> otherAddonRooms = [];
    private static List<PublicRoomBrief> otherVersionRooms = [];

    public static int OtherAddonCount { get; private set; } = 0;

    public static int OtherVersionCount { get; private set; } = 0;

    private static float lastFetchTime = float.NegativeInfinity;
    private static bool fetching = false;

    public static bool ShouldNotRefetchNow => Time.realtimeSinceStartup - lastFetchTime < PublicRoomBrowser.RefetchIntervalSeconds;

    private static HashSet<string>? CurrentRegionEndpoints()
    {
        var region = ServerManager.InstanceExists ? ServerManager.Instance.CurrentRegion : null;
        if (region == null) return null;

        HashSet<string> endpoints = [];
        foreach (var server in region.Servers.GetFastEnumerator()) endpoints.Add(server.Ip + ":" + server.Port);
        return endpoints;
    }

    // 将来的にNoSサーバー以外の掲載依頼を受け付けるつもりがあるので、絞り込みは行っておく。
    public static List<PublicRoomEntry> RoomsInCurrentRegion()
    {
        var endpoints = CurrentRegionEndpoints();
        if (endpoints == null) return [];

        return allRooms.Where(room => endpoints.Contains(room.RegionAddr + ":" + room.RegionPort)).ToList();
    }

    private static IEnumerable<PublicRoomBrief> InCurrentRegion(List<PublicRoomBrief> rooms)
    {
        var endpoints = CurrentRegionEndpoints();
        if (endpoints == null) return [];

        return rooms.Where(room => endpoints.Contains(room.RegionAddr + ":" + room.RegionPort));
    }

    public static IEnumerable<PublicRoomBrief> OtherAddonRooms() => otherAddonRooms;

    public static IEnumerable<PublicRoomBrief> OtherVersionRooms() => otherVersionRooms;

    public static int UnjoinableRoomCount() => OtherAddonCount + OtherVersionCount;

    public static IEnumerable<PublicRoomBrief> OtherAddonRoomsInCurrentRegion() => InCurrentRegion(otherAddonRooms);

    public static IEnumerable<PublicRoomBrief> OtherVersionRoomsInCurrentRegion() => InCurrentRegion(otherVersionRooms);

    public static int UnjoinableRoomCountInCurrentRegion() => OtherAddonRoomsInCurrentRegion().Count() + OtherVersionRoomsInCurrentRegion().Count();

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
                OtherAddonCount = response.OtherAddons;
                otherAddonRooms = (response.OtherAddonRooms ?? []).ToList();
                OtherVersionCount = response.OtherVersions;
                otherVersionRooms = (response.OtherVersionRooms ?? []).ToList();
                lastFetchTime = Time.realtimeSinceStartup;
                succeeded = true;
            });

        fetching = false;
        onDone.Invoke(succeeded);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nebula.Online;

internal sealed class NoSPlayerRef
{
    [JsonPropertyName("uid")] public string Uid { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class SocialResponse
{
    [JsonPropertyName("resultId")] public int ResultId { get; set; } = int.MinValue;
    [JsonPropertyName("blocked")] public string[] Blocked { get; set; } = [];
    [JsonPropertyName("friends")] public NoSPlayerRef[] Friends { get; set; } = [];
    [JsonPropertyName("requests")] public NoSPlayerRef[] Requests { get; set; } = [];
}

internal static class NoSSocial
{
    private static readonly HashSet<string> blocked = [];
    private static readonly Dictionary<string, string> friends = [];
    private static readonly Dictionary<string, string> requests = [];

    public static bool IsLoaded { get; private set; }

    public static event Action? OnUpdated;

    public static IReadOnlyCollection<string> Blocked => blocked;

    public static IEnumerable<NoSPlayerRef> Friends =>
        friends.Select(e => new NoSPlayerRef { Uid = e.Key, Name = e.Value });

    public static IEnumerable<NoSPlayerRef> Requests =>
        requests.Select(e => new NoSPlayerRef { Uid = e.Key, Name = e.Value });

    public static bool IsBlocked(string uid) => blocked.Contains(uid);

    public static bool IsFriend(string uid) => friends.ContainsKey(uid);

    public static bool HasRequestFrom(string uid) => requests.ContainsKey(uid);

    public static string NameOf(string uid) =>
        friends.TryGetValue(uid, out var name) || requests.TryGetValue(uid, out name) ? name : "";

    public static void Clear()
    {
        blocked.Clear();
        friends.Clear();
        requests.Clear();
        IsLoaded = false;
        Changed();
    }

    public static void Load(SocialResponse response)
    {
        blocked.Clear();
        friends.Clear();
        requests.Clear();

        foreach (var uid in response.Blocked ?? []) blocked.Add(uid);
        foreach (var entry in response.Friends ?? []) friends[entry.Uid] = entry.Name ?? "";
        foreach (var entry in response.Requests ?? []) requests[entry.Uid] = entry.Name ?? "";

        IsLoaded = true;
        Changed();
    }

    public static void ApplyBlocked(string uid)
    {
        blocked.Add(uid);
        friends.Remove(uid);
        requests.Remove(uid);
        Changed();
    }

    public static void ApplyUnblocked(string uid)
    {
        blocked.Remove(uid);
        Changed();
    }

    public static void ApplyRequestSent(string uid)
    {
        if (requests.Remove(uid, out var name)) friends[uid] = name;
        Changed();
    }

    public static void ApplyAccepted(string uid, string name)
    {
        requests.Remove(uid);
        friends[uid] = name ?? "";
        Changed();
    }

    public static void ApplyDenied(string uid)
    {
        requests.Remove(uid);
        Changed();
    }

    public static void ApplyFriendRemoved(string uid)
    {
        friends.Remove(uid);
        Changed();
    }

    private static void Changed()
    {
        try
        {
            OnUpdated?.Invoke();
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoSSocial: an update handler threw. " + e.Message);
        }
    }
}

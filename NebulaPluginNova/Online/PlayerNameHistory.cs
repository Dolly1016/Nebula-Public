using Nebula.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Online;

internal class NameHistoryEntry
{
    [JsonSerializableField]
    public List<string> Names = [];

    [JsonSerializableField]
    public bool IsFriend = false;

    [JsonSerializableField]
    public bool IsBlocked = false;

    [JsonSerializableField]
    public string BlockedName = "";

    [JsonSerializableField]
    public string FriendName = "";
}

internal class NameHistoryStructure
{
    [JsonSerializableField]
    public Dictionary<string, NameHistoryEntry> Histories = [];
}

internal readonly struct PlayerObservation
{
    public string Uid { get; init; }
    public string Name { get; init; }
    public bool IsFriend { get; init; }
    public bool IsBlocked { get; init; }
    public bool IsKnownUid { get; init; }
    public string PreviousName { get; init; }
    public IReadOnlyList<string> OtherUids { get; init; }
    public int Experience { get; init; }
    public bool UsedByOthers => OtherUids.Count > 0;
    public bool UsedByFriends { get; init; }
}

internal sealed class NameHistory
{
    public const int MaxNamesPerUid = 16;
    private const int MaxNameLength = 32;

    private readonly Dictionary<string, NameHistoryEntry> histories;
    private readonly Dictionary<string, HashSet<string>> uidsByName = [];

    public NameHistory(Dictionary<string, NameHistoryEntry>? source = null)
    {
        histories = source ?? [];
        RebuildIndex();
    }

    private void RebuildIndex()
    {
        uidsByName.Clear();
        foreach (var (uid, entry) in histories)
        {
            foreach (var name in entry.Names)
            {
                if (!uidsByName.TryGetValue(name, out var uids)) uidsByName[name] = uids = [];
                uids.Add(uid);
            }
        }
    }

    private static string Normalize(string? name)
    {
        if (name == null) return "";
        var stripped = StripLobbySuffix(name.Trim());
        return stripped.Length > MaxNameLength ? stripped[..MaxNameLength] : stripped;
    }

    private static string StripLobbySuffix(string name)
    {
        var space = name.LastIndexOf(' ');
        if (space <= 0 || space == name.Length - 1) return name;

        for (var i = space + 1; i < name.Length; i++)
        {
            if (name[i] < '0' || name[i] > '9') return name;
        }
        return name[..space].TrimEnd();
    }

    public IReadOnlyList<string> HistoryOf(string uid) => histories.TryGetValue(uid, out var entry) ? entry.Names : [];

    public string CurrentNameOf(string uid) => histories.TryGetValue(uid, out var entry) ? LatestOf(entry) : "";

    private static string LatestOf(NameHistoryEntry entry) => entry.IsFriend ? entry.FriendName : entry.IsBlocked ? entry.BlockedName : "";

    public string FriendNameOf(string uid) => histories.TryGetValue(uid, out var entry) ? entry.FriendName : "";

    public string BlockedNameOf(string uid) => histories.TryGetValue(uid, out var entry) ? entry.BlockedName : "";

    public bool WasFriend(string uid) => histories.TryGetValue(uid, out var entry) && entry.IsFriend;

    public bool WasBlocked(string uid) => histories.TryGetValue(uid, out var entry) && entry.IsBlocked;

    public IReadOnlyCollection<string> UidsOf(string name) => uidsByName.TryGetValue(Normalize(name), out var uids) ? uids : [];

    public PlayerObservation? Observe(NebulaAuthEntry auth, NetworkedPlayerInfo relatedPlayer) => auth.Uid != null ? Observe(auth.Uid, relatedPlayer.PlayerName, auth.Experience) : null;

    public PlayerObservation Observe(string uid, string name, int experience = -1)
    {
        var normalized = Normalize(name);
        histories.TryGetValue(uid, out var entry);
        var previous = entry == null ? "" : LatestOf(entry);
        var others = OthersUsing(normalized, uid);

        return new PlayerObservation
        {
            Uid = uid,
            Name = normalized,
            IsFriend = entry?.IsFriend ?? false,
            IsBlocked = entry?.IsBlocked ?? false,
            IsKnownUid = entry != null,
            PreviousName = previous,
            OtherUids = others,
            UsedByFriends = others.Any(NoSSocial.IsFriend),
            Experience = experience,
        };
    }

    private IReadOnlyList<string> OthersUsing(string normalized, string uid)
    {
        if (!uidsByName.TryGetValue(normalized, out var uids)) return Array.Empty<string>();
        if (uids.Count == 0 || (uids.Count == 1 && uids.Contains(uid))) return Array.Empty<string>();

        return uids.Where(u => u != uid).ToArray();
    }

    public bool Record(in PlayerObservation observation, bool? asFriend, bool? asBlocked)
    {
        var uid = observation.Uid;
        if (string.IsNullOrEmpty(uid)) return false;

        var normalized = observation.Name;
        if (normalized.Length == 0) return false;

        if (!histories.TryGetValue(uid, out var entry)) histories[uid] = entry = new NameHistoryEntry();

        var changed = false;

        if (LatestOf(entry) != normalized)
        {
            entry.Names.Remove(normalized);
            entry.Names.Add(normalized);
            TrimNames(uid, entry);

            if (!uidsByName.TryGetValue(normalized, out var uids)) uidsByName[normalized] = uids = [];
            uids.Add(uid);
            changed = true;
        }

        if (asFriend.HasValue && entry.IsFriend != asFriend.Value)
        {
            entry.IsFriend = asFriend.Value;
            entry.FriendName = asFriend.Value ? normalized : "";
            changed = true;
        }
        else if (entry.IsFriend && entry.FriendName.Length == 0)
        {
            entry.FriendName = normalized;
            changed = true;
        }

        if (asBlocked.HasValue && entry.IsBlocked != asBlocked.Value)
        {
            entry.IsBlocked = asBlocked.Value;
            entry.BlockedName = asBlocked.Value ? normalized : "";
            changed = true;
        }
        else if (entry.IsBlocked && entry.BlockedName.Length == 0)
        {
            entry.BlockedName = normalized;
            changed = true;
        }

        return changed;
    }

    public bool RecordBlocked(string uid, string name) => Record(Observe(uid, name), null, true);

    /// <summary>
    /// フレンドになった時点の表示名を控える。申請を出したときと、申請を承認したときに呼ぶ。
    /// </summary>
    /// <remarks>
    /// 申請は相手が承認するまで成立しないが、名前を知れるのは送った時点なのでここで押さえる。
    /// 既に控えてある名前があっても、この経路で渡された名前を正とする。
    /// </remarks>
    public bool RecordFriend(string uid, string name)
    {
        var observation = Observe(uid, name);
        var changed = Record(observation, true, null);

        if (histories.TryGetValue(uid, out var entry) && entry.FriendName != observation.Name)
        {
            entry.FriendName = observation.Name;
            changed = true;
        }
        return changed;
    }

    private void TrimNames(string uid, NameHistoryEntry entry)
    {
        while (entry.Names.Count > MaxNamesPerUid)
        {
            var dropped = entry.Names[0];
            entry.Names.RemoveAt(0);
            if (!entry.Names.Contains(dropped) && uidsByName.TryGetValue(dropped, out var holders))
            {
                holders.Remove(uid);
                if (holders.Count == 0) uidsByName.Remove(dropped);
            }
        }
    }

    public void Forget(string uid)
    {
        if (!histories.Remove(uid, out var entry)) return;
        foreach (var name in entry.Names)
        {
            if (!uidsByName.TryGetValue(name, out var uids)) continue;
            uids.Remove(uid);
            if (uids.Count == 0) uidsByName.Remove(name);
        }
    }
}

internal static class PlayerNameHistory
{
    private static readonly JsonDataSaver<NameHistoryStructure> Saver = new("NoSNameHistory");

    private static NameHistory? cached;

    public static NameHistory Instance => cached ??= new NameHistory(Saver.Data!.Histories);

    public static IReadOnlyList<string> HistoryOf(string uid) => Instance.HistoryOf(uid);

    public static string CurrentNameOf(string uid) => Instance.CurrentNameOf(uid);

    public static string BlockedNameOf(string uid) => Instance.BlockedNameOf(uid);

    public static string FriendNameOf(string uid) => Instance.FriendNameOf(uid);

    public static bool WasFriend(string uid) => Instance.WasFriend(uid);

    public static bool WasBlocked(string uid) => Instance.WasBlocked(uid);

    public static IReadOnlyCollection<string> UidsOf(string name) => Instance.UidsOf(name);

    public static PlayerObservation Observe(string uid, string name, int experience = -1) => Instance.Observe(uid, name, experience);
    public static PlayerObservation? Observe(NebulaAuthEntry auth, NetworkedPlayerInfo player) => Instance.Observe(auth, player);

    public static void Record(in PlayerObservation observation)
    {
        if (Instance.Record(observation, null, null)) Saver.Save();
    }
    
    public static void RecordBlocked(string uid, string name)
    {
        if (Instance.RecordBlocked(uid, name)) Saver.Save();
    }

    public static void RecordFriend(string uid, string name)
    {
        if (Instance.RecordFriend(uid, name)) Saver.Save();
    }

    public static void Forget(string uid)
    {
        Instance.Forget(uid);
        Saver.Save();
    }
}

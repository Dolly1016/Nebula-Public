using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Online;

internal static class PlayerNameCheck
{
    private static readonly HashSet<int> pending = [];

    public static void Reset()
    {
        pending.Clear();
    }

    public static void Register(int clientId)
    {
        pending.Add(clientId);
    }

    public static void CheckPending()
    {
        if (pending.Count == 0) return;
        pending.RemoveWhere(clientId => TryCheck(clientId));
    }

    private static bool TryCheck(int clientId)
    {
        var entry = NoSAuth.Get(clientId);
        if (!entry.IsVerified || string.IsNullOrEmpty(entry.Uid)) return true;
        
        var info = FindPlayer(clientId);
        if (!info.AsBoolFast()) return true;
        
        var name = info!.PlayerName;
        if (string.IsNullOrEmpty(name)) return false;
        
        var uid = entry.Uid!;

        var observation = PlayerNameHistory.Observe(uid, name);
        PlayerNameHistory.Record(observation);
        RecentPlayers.Record(uid, observation.Name);

        return true;
    }

    private static NetworkedPlayerInfo? FindPlayer(int clientId)
    {
        if (!GameData.Instance.AsBoolFast()) return null;

        foreach (var info in GameData.Instance.AllPlayers.GetFastEnumerator())
        {
            if (info.AsBoolFast() && info.ClientId == clientId) return info;
        }
        return null;
    }
}

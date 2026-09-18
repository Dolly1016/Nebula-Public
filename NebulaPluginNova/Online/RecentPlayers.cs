using System.Collections.Generic;

namespace Nebula.Online;

/// <summary>
/// このプレイ中に居合わせた、認証済みプレイヤーの控え。
/// </summary>
/// <remarks>
/// ブロックとフレンド申請に要るのは uid と、その時に名乗っていた名前だけ。
/// 会った順に並び、同じ uid に会い直すと末尾へ移る。ゲームを閉じると消える。
/// </remarks>
internal static class RecentPlayers
{
    private const int Capacity = 50;

    private static readonly List<NoSPlayerRef> players = [];

    /// <summary>直近に会った相手から順に返す。</summary>
    public static IEnumerable<NoSPlayerRef> Newest
    {
        get
        {
            for (var index = players.Count - 1; index >= 0; index--) yield return players[index];
        }
    }

    /// <summary>認証で uid が確定し、表示名も揃った相手を控える。</summary>
    public static void Record(string uid, string name)
    {
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(name)) return;

        var myUid = NoSIdentity.Instance?.UId ?? "";
        if (uid == myUid) return;

        players.RemoveAll(player => player.Uid == uid);
        players.Add(new NoSPlayerRef { Uid = uid, Name = name });

        if (players.Count > Capacity) players.RemoveRange(0, players.Count - Capacity);
    }
}

using Nebula.Online;

namespace Nebula.Patches;

// ゲーム内のフレンドリストを NoS のフレンド・ブロックへ差し替える。
// 処理の中身は Online/FriendListView にある。
// プラットフォームフレンドのタブには手を出さない。

[HarmonyPatch(typeof(FriendsListManager), nameof(FriendsListManager.RefreshFriendsList))]
public static class FriendListRefreshListsPatch
{
    public static bool Prefix(
        ref Il2CppSystem.Collections.IEnumerator __result,
        [HarmonyArgument(0)] Il2CppSystem.Action cb1)
    {
        __result = FriendListView.CoRefreshLists(() => cb1?.Invoke()).WrapToIl2Cpp();
        return false;
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.RefreshFriends))]
public static class FriendListRefreshFriendsPatch
{
    public static bool Prefix(FriendsListUI __instance)
    {
        FriendListView.RefreshFriends(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.RefreshBlockedPlayers))]
public static class FriendListRefreshBlockedPatch
{
    public static bool Prefix(FriendsListUI __instance)
    {
        FriendListView.RefreshBlocked(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.RefreshNotifications))]
public static class FriendListRefreshNotificationsPatch
{
    public static bool Prefix(FriendsListUI __instance)
    {
        FriendListView.RefreshNotifications(__instance);
        return false;
    }
}

// ---------------------------------------------------------------- ロビーの参加者

[HarmonyPatch(typeof(LobbyPlayerBar), nameof(LobbyPlayerBar.CheckAddFriend))]
public static class LobbyPlayerBarAddFriendPatch
{
    public static bool Prefix(LobbyPlayerBar __instance)
    {
        FriendListView.SendFriendRequest(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(LobbyPlayerBar), nameof(LobbyPlayerBar.CheckBlockPlayer))]
public static class LobbyPlayerBarBlockPatch
{
    public static bool Prefix(LobbyPlayerBar __instance)
    {
        FriendListView.Block(__instance);
        return false;
    }
}

// ---------------------------------------------------------------- フレンド

[HarmonyPatch(typeof(OnlineFriendBar), nameof(OnlineFriendBar.CheckRemoveFriend))]
public static class OnlineFriendBarRemovePatch
{
    public static bool Prefix(OnlineFriendBar __instance)
    {
        FriendListView.RemoveFriend(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(OnlineFriendBar), nameof(OnlineFriendBar.CheckBlockPlayer))]
public static class OnlineFriendBarBlockPatch
{
    public static bool Prefix(OnlineFriendBar __instance)
    {
        FriendListView.Block(__instance);
        return false;
    }
}

// ---------------------------------------------------------------- ブロック済み

[HarmonyPatch(typeof(BlockedPlayerBar), nameof(BlockedPlayerBar.CheckUnblockPlayer))]
public static class BlockedPlayerBarUnblockPatch
{
    public static bool Prefix(BlockedPlayerBar __instance)
    {
        FriendListView.Unblock(__instance);
        return false;
    }
}

// ---------------------------------------------------------------- 受け取った申請

[HarmonyPatch(typeof(FriendRequestBar), nameof(FriendRequestBar.PressAccept))]
public static class FriendRequestBarAcceptPatch
{
    public static bool Prefix(FriendRequestBar __instance)
    {
        FriendListView.AcceptRequest(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FriendRequestBar), nameof(FriendRequestBar.DenyFriendRequest))]
public static class FriendRequestBarDenyPatch
{
    public static bool Prefix(FriendRequestBar __instance)
    {
        FriendListView.DenyRequest(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FriendRequestBar), nameof(FriendRequestBar.CheckBlockPlayer))]
public static class FriendRequestBarBlockPatch
{
    public static bool Prefix(FriendRequestBar __instance)
    {
        FriendListView.Block(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.RefreshLobbyPlayers))]
public static class FriendListRefreshLobbyPatch
{
    public static bool Prefix(FriendsListUI __instance)
    {
        FriendListView.RefreshLobbyPlayers(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.RefreshRecentlyPlayed))]
public static class FriendListRefreshRecentPatch
{
    public static bool Prefix(FriendsListUI __instance)
    {
        FriendListView.RefreshRecentlyPlayed(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(LobbyPlayerBar), nameof(LobbyPlayerBar.SetUp))]
public static class LobbyPlayerBarSetUpPatch
{
    public static void Postfix(LobbyPlayerBar __instance) => FriendListView.SetUpLobbyPlayerBar(__instance);
}

[HarmonyPatch(typeof(LobbyPlayerBar), nameof(LobbyPlayerBar.ReportPlayer))]
public static class LobbyPlayerBarReportPatch
{
    // 報告は Innersloth のサーバーへ送るもの。ボタンごと出さないが、念のため塞ぐ
    public static bool Prefix() => false;
}

[HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.AddFriend))]
public static class FriendListAddByCodePatch
{
    // NoS にフレンドコードは無い。入力欄ごと出さないが、念のため塞ぐ
    public static bool Prefix() => false;
}

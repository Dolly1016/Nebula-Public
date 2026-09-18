using AmongUs.Data;
using Nebula.Modules;
using Assets.InnerNet;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nebula.Online;

internal static class FriendListView
{
    private static string LocalName => DataManager.Player.Customization.Name ?? "";

    private static void Run(IEnumerator routine) => NebulaManager.Instance.StartCoroutine(routine.WrapToIl2Cpp());

    private static string UidOf(FriendsListBar bar) => bar.puid ?? "";

    private static FriendsListUI? Ui => DestroyableSingleton<FriendsListManager>.Instance.Ui;

    private static void Rebuild()
    {
        var ui = Ui;
        if (!ui.AsBoolFast() || !ui!.IsOpen) return;

        ui.UpdateFriendBars();
    }

    public static IEnumerator CoRefreshLists(System.Action? onDone)
    {
        yield return NoSApi.CoLoadSocial(_ => { });
        onDone?.Invoke();
    }

    public static void HideAddFriendByCode(FriendsListUI ui)
    {
        if (ui.transform.TryFindChild(out var buttonObj, "Menu", "Tab Contents", "AU Friends", 0, 2)) buttonObj.gameObject.SetActive(false);
    }

    public static void RefreshFriends(FriendsListUI ui)
    {
        HideAddFriendByCode(ui);

        var index = 0;
        foreach (var friend in NoSSocial.Friends)
        {
            var bar = GameObject.Instantiate(ui.OnlineFriendBar, ui.FriendArea.transform);
            bar.SetUp(friend.Uid, ui, "", friend.Name);

            bar.LobbyInviteButton.gameObject.SetActive(false);

            Place(bar, ui, ui.FriendsScroller, index++);
        }
        SetBounds(ui, ui.FriendsScroller, index);
    }

    public static void RefreshBlocked(FriendsListUI ui)
    {
        var index = 0;
        foreach (var uid in NoSSocial.Blocked)
        {
            var bar = GameObject.Instantiate(ui.BlockedPlayerBar, ui.BlockedArea.transform);
            bar.SetUp(uid, ui, "", NameFor(uid));
            Place(bar, ui, ui.BlockedScroller, index++);
        }
        SetBounds(ui, ui.BlockedScroller, index);
    }

    public static void RefreshNotifications(FriendsListUI ui)
    {
        var index = 0;
        foreach (var request in NoSSocial.Requests)
        {
            var bar = GameObject.Instantiate(ui.FriendRequestBar, ui.NotifArea.transform);
            bar.SetUp(request.Uid, ui, "", request.Name);
            Place(bar, ui, ui.NotifScroller, index++);
        }
        SetBounds(ui, ui.NotifScroller, index);
    }

    // ロビーのプレイヤー一覧
    public static void RefreshLobbyPlayers(FriendsListUI ui)
    {
        if (!GameData.Instance.AsBoolFast()) return;
        if (!AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer)) return;

        var index = 0;
        foreach (var info in GameData.Instance.AllPlayers.GetFastEnumerator())
        {
            if (!info.AsBoolFast() || info.PlayerId == localPlayer.PlayerId) continue;

            var auth = NoSAuth.Get(info.ClientId);
            var uid = auth.Uid ?? "";
            var bar = GameObject.Instantiate(ui.LobbyPlayerBar, ui.LobbyPlayerArea.transform);
            bar.SetUp(uid, ui, uid, info.PlayerName ?? "");
            SetLobbyStatus(bar, uid, PlayerNameHistory.Observe(auth, info));
            Place(bar, ui, ui.LobbyScroller, index++);
        }
        SetBounds(ui, ui.LobbyScroller, index);
    }

    public static void RefreshRecentlyPlayed(FriendsListUI ui)
    {
        var index = 0;
        foreach (var player in RecentPlayers.Newest)
        {
            var bar = GameObject.Instantiate(ui.LobbyPlayerBar, ui.RecentlyPlayedArea.transform);
            bar.SetUp(player.Uid, ui, player.Uid, player.Name);
            Place(bar, ui, ui.RecentlyPlayedScroller, index++);
        }
        SetBounds(ui, ui.RecentlyPlayedScroller, index);
    }

    /// <summary>
    /// 1 人分のバーの見た目を NoS の状態に合わせる。
    /// </summary>
    /// <remarks>
    /// 報告は Innersloth のサーバーへ送るものなので出さない。
    /// フレンド追加とブロックは、相手の uid が分かっているときだけ押せるようにする。
    /// </remarks>
    public static void SetUpLobbyPlayerBar(LobbyPlayerBar bar)
    {
        var uid = UidOf(bar);

        bar.ReportButton.gameObject.SetActive(false);

        var usable = uid.Length > 0;
        bar.AddFriendButton.gameObject.SetActive(usable);
        bar.BlockButton.gameObject.SetActive(usable);
        if (!usable) return;

        bar.IsFriend = NoSSocial.IsFriend(uid);
        bar.IsBlocked = NoSSocial.IsBlocked(uid);
        bar.UpdateStatus();
    }

    private const int BeginnerExperience = 1;

    private static void SetLobbyStatus(LobbyPlayerBar bar, string uid, PlayerObservation? observation)
    {
        bar.SenderName.text = StatusOf(uid, observation);
    }

    private static string StatusOf(string uid, PlayerObservation? observation)
    {
        if (uid.Length == 0) return "";

        if (NoSSocial.IsFriend(uid)) return WithPreviousName("ui.friendList.friend", observation);
        if (NoSSocial.IsBlocked(uid)) return WithPreviousName("ui.friendList.blocked", observation);

        if (!observation.HasValue) return "";
        var data = observation.Value;

        if (data.UsedByFriends) return Language.Translate("ui.friendList.sameNameAsFriend");
        if (data.UsedByOthers) return Language.Translate("ui.friendList.sameNameAsKnown");
        if (data.Experience >= 0 && data.Experience <= BeginnerExperience) return Language.Translate("ui.friendList.beginner");

        return "";
    }

    /// <summary>改名していれば、以前の名前を添えた方の訳を返す。</summary>
    private static string WithPreviousName(string key, PlayerObservation? observation)
    {
        if (!observation.HasValue) return Language.Translate(key);

        var data = observation.Value;
        if (data.PreviousName == data.Name) return Language.Translate(key);

        return Language.Translate(key + "Renamed").Replace("%NAME%", data.PreviousName);
    }

    private static string NameFor(string uid)
    {
        var known = NoSSocial.NameOf(uid);
        if (known.Length > 0) return known;

        var blocked = PlayerNameHistory.BlockedNameOf(uid);
        if (blocked.Length > 0) return blocked;

        var friend = PlayerNameHistory.FriendNameOf(uid);
        if (friend.Length > 0) return friend;

        var seen = PlayerNameHistory.CurrentNameOf(uid);
        return seen.Length > 0 ? seen : "(Unknown User)";
    }

    private static string NameFor(FriendsListBar bar) => NameFor(UidOf(bar));

    private static void Place(FriendsListBar bar, FriendsListUI ui, Scroller scroller, int index)
    {
        bar.transform.localPosition = new Vector3(-0.29f, ui.YStart - index * ui.YOffset, -1f);
        foreach (var button in bar.Buttons) button.ClickMask = scroller.Hitbox;

        if (bar.PlatformIdentifier.AsBoolFast()) bar.PlatformIdentifier.platformIDText.text = "";
    }

    private static void SetBounds(FriendsListUI ui, Scroller scroller, int count) =>
        scroller.SetYBoundsMax(-(ui.YStart - count * ui.YOffset));

    public static void SendFriendRequest(FriendsListBar bar)
    {
        var uid = UidOf(bar);
        if (uid.Length == 0) return;

        // 相手の名前を控える
        var name = NameFor(bar);

        Confirm(bar, FriendsListConfirmMenu.ActionType.Friend,
            () => Run(NoSApi.CoSendFriendRequest(uid, LocalName, result =>
            {
                if (result.Ok) PlayerNameHistory.RecordFriend(uid, name);
                Rebuild();
            })));
    }

    public static void Block(FriendsListBar bar)
    {
        var uid = UidOf(bar);
        if (uid.Length == 0) return;

        var name = NameFor(bar);

        Confirm(bar, FriendsListConfirmMenu.ActionType.Block,
            () => Run(NoSApi.CoBlock(uid, result =>
            {
                if (result.Ok) PlayerNameHistory.RecordBlocked(uid, name);
                Rebuild();
            })));
    }

    public static void Unblock(FriendsListBar bar)
    {
        var uid = UidOf(bar);
        if (uid.Length == 0) return;

        Confirm(bar, FriendsListConfirmMenu.ActionType.Unblock,
            () => Run(NoSApi.CoUnblock(uid, _ => Rebuild())));
    }

    public static void RemoveFriend(FriendsListBar bar)
    {
        var uid = UidOf(bar);
        if (uid.Length == 0) return;

        Confirm(bar, FriendsListConfirmMenu.ActionType.Unfriend,
            () => Run(NoSApi.CoRemoveFriend(uid, _ => Rebuild())));
    }

    public static void AcceptRequest(FriendsListBar bar)
    {
        var uid = UidOf(bar);
        if (uid.Length == 0 || !bar.CanUseApi()) return;

        var sentName = NoSSocial.NameOf(uid);

        Run(NoSApi.CoAcceptFriend(uid, LocalName, result =>
        {
            if (result.Ok) PlayerNameHistory.RecordFriend(uid, result.Name.Length > 0 ? result.Name : sentName);
            Rebuild();
        }));
    }

    public static void DenyRequest(FriendsListBar bar)
    {
        var uid = UidOf(bar);
        if (uid.Length == 0 || !bar.CanUseApi()) return;

        Run(NoSApi.CoDenyFriend(uid, _ => Rebuild()));
    }

    private static void Confirm(FriendsListBar bar, FriendsListConfirmMenu.ActionType type, System.Action action)
    {
        if (!bar.CanUseApi()) return;

        DestroyableSingleton<FriendsListManager>.Instance.OpenConfirmationScreen(
            action, type, NameFor(UidOf(bar)));
    }
}

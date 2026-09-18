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

internal static class PublicRoomBrowserView
{
    // バニラのフィルタ取得を行わせない。
    public static IEnumerator CoSkipFilters(Il2CppSystem.Action<PermittedFilters> onRefreshFilters)
{
        onRefreshFilters?.Invoke(null);
        yield break;
    }

    // 一覧を組み直す。バニラの処理を行わせないため、falseを返す。
    public static bool RefreshList(FindAGameManager manager)
    {
        if (manager.timer < 1f) return false;
        manager.timer = 0f;

        if (NoSIdentity.Instance == null)
        {
            Render(manager, [], 0);
            return false;
        }

        if (!DestroyableSingleton<MatchMaker>.Instance.Connecting<FindAGameManager>(manager)) return false;

        if (PublicRoomDirectory.ShouldNotRefetchNow)
        {
            Render(manager, PublicRoomDirectory.RoomsInCurrentRegion(), PublicRoomDirectory.UnjoinableRoomCountInCurrentRegion());
            return false;
        }

        manager.SetRefresh(false);
        manager.ResetContainers();
        manager.StartIcon();

        manager.StartCoroutine(CoRefresh(manager).WrapToIl2Cpp());
        return false;
    }

    private static IEnumerator CoRefresh(FindAGameManager manager)
    {
        yield return PublicRoomDirectory.CoFetch(_ => { });

        if (manager == null) yield break;
        Render(manager, PublicRoomDirectory.RoomsInCurrentRegion(), PublicRoomDirectory.UnjoinableRoomCountInCurrentRegion());
    }

    internal static void Render(FindAGameManager manager, List<PublicRoomEntry> rooms, int unjoinableRooms)
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

            manager.TotalText.text = rooms.Count.ToString() + (unjoinableRooms > 0 ? $" +{unjoinableRooms}".Sized(60) : "");
            //manager.matchesFoundText.text = rooms.Count.ToString();
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

    // 1 件分の表示
    public static void DecorateContainer(GameContainer container)
    {
        if (!PublicRoomListingConverter.TryGet(container.gameListing.GameId, out var room)) return;

        try
        {
            var title = string.IsNullOrEmpty(room.Title) ? room.HostName : room.Title;
            container.tag1.text = title;
            container.tag2.text = room.HostName;
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to draw a public room entry. " + e.Message);
        }
    }

    // 詳細ポップアップ表示
    public static void DecorateMoreInfo(FindGameMoreInfoPopup popup, GameListing listing)
    {
        if (!PublicRoomListingConverter.TryGet(listing.GameId, out var room)) return;

        try
        {
            popup.regionText.text = room.HostName;
            popup.languageText.text = Modules.Language.GetLanguage(room.Lang);
        }
        catch (Exception e)
        {
            LogUtils.WriteToConsole("NoS: failed to draw the public room details. " + e.Message);
        }
    }

    // 一覧画面を開いたときの作り替え
    public static void SetUpBrowser(FindAGameManager manager)
    {
        var titleAttr = new TextAttribute(Virial.Text.TextAlignment.Left, GUI.API.GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(2.5f, 1.5f, 2.5f), new(3f, 0.5f), new(255, 255, 255), false);
        var hostAttr = new TextAttribute(Virial.Text.TextAlignment.Left, GUI.API.GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.6f, 1.3f, 1.6f), new(3f, 0.3f), new(255, 255, 255), false);

        var titleComponent = GUI.API.RawText(Virial.Media.GUIAlignment.Left, titleAttr, "");
        var hostComponent = GUI.API.RawText(Virial.Media.GUIAlignment.Left, hostAttr, "");

        foreach (var container in manager.gameContainers.GetFastEnumerator())
        {
            container.gameObject.AddComponent<SortingGroup>();

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

        //フィルタ関係の非表示
        manager.clearFilterButton.gameObject.SetActive(false); // ClearFilterButton
        manager.container.GetChild(2).gameObject.SetActive(false); // Filters
        manager.container.GetChild(4).GetChild(0).gameObject.SetActive(false); // Content/FiltersTab
    }

    // ゲームモード強制
    public static void ForceNormalGameMode()
    {
        if (DataManager.Settings.Multiplayer.LastPlayedGameMode != AmongUs.GameOptions.GameModes.Normal)
        {
            DataManager.Settings.Multiplayer.LastPlayedGameMode = AmongUs.GameOptions.GameModes.Normal;
        }
    }
}

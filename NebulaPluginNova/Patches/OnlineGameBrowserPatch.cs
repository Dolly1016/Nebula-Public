using AmongUs.Matchmaking;
using InnerNet;
using Nebula.Online;

namespace Nebula.Patches;

// フィルタリフレッシュ
[HarmonyPatch(typeof(HttpMatchmakerManager), nameof(HttpMatchmakerManager.CoRefreshFilters))]
public static class PublicRoomSkipRefreshFiltersPatch
{
    public static bool Prefix(ref Il2CppSystem.Collections.IEnumerator __result, [HarmonyArgument(0)] Il2CppSystem.Action<PermittedFilters> onRefreshFilters)
    {
        __result = PublicRoomBrowserView.CoSkipFilters(onRefreshFilters).WrapToIl2Cpp();
        return false;
    }
}

// リストリフレッシュ
[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.RefreshList))]
public static class PublicRoomRefreshListPatch
{
    public static bool Prefix(FindAGameManager __instance) => PublicRoomBrowserView.RefreshList(__instance);
}

// ゲーム一覧セットアップ
[HarmonyPatch(typeof(GameContainer), nameof(GameContainer.SetupGameInfo))]
public static class PublicRoomGameContainerPatch
{
    public static void Postfix(GameContainer __instance) => PublicRoomBrowserView.DecorateContainer(__instance);
}

// ゲーム詳細情報セットアップ
[HarmonyPatch(typeof(FindGameMoreInfoPopup), nameof(FindGameMoreInfoPopup.SetupInfo))]
public static class PublicRoomMoreInfoPatch
{
    public static void Postfix(FindGameMoreInfoPopup __instance, [HarmonyArgument(0)] GameListing gameL) =>
        PublicRoomBrowserView.DecorateMoreInfo(__instance, gameL);
}

// ゲーム一覧閲覧終了
[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.OnDestroy))]
public static class PublicRoomLeaveBrowserPatch
{
    public static void Postfix() => PublicRoomDirectory.DeleteCache();
}

// ゲーム一覧閲覧開始
[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
public static class PublicRoomStartBrowserPatch
{
    public static void Prefix(FindAGameManager __instance) => PublicRoomBrowserView.ForceNormalGameMode();

    public static void Postfix(FindAGameManager __instance) => PublicRoomBrowserView.SetUpBrowser(__instance);
}

using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;

namespace Nebula.Preloader;

public static class SteamOverlay
{
    private const string SteamApi = "steam_api";

    // Among Us の steam_api(64).dll は既定の DLL 検索パスに含まれない Plugins フォルダにあるため、フルパスで解決する。
    private static string SteamApiPath => Environment.Is64BitProcess
        ? Path.Combine(Paths.GameDataPath, "Plugins", "x86_64", "steam_api64.dll")
        : Path.Combine(Paths.GameDataPath, "Plugins", "x86", "steam_api.dll");

    static SteamOverlay()
    {
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(),
            (name, _, _) => name == SteamApi && NativeLibrary.TryLoad(SteamApiPath, out var handle) ? handle : IntPtr.Zero);
    }

    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_Init();

    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamFriends_v017();

    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamUtils_v010();

    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_ISteamFriends_ActivateGameOverlayToWebPage(IntPtr self, [MarshalAs(UnmanagedType.LPUTF8Str)] string url, int mode);

    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUtils_IsOverlayEnabled(IntPtr self);

    internal static bool Init() => SteamAPI_Init();

    public static bool OpenWebPage(string url)
    {
        var utils = SteamAPI_SteamUtils_v010();
        if (utils == IntPtr.Zero || !SteamAPI_ISteamUtils_IsOverlayEnabled(utils)) return false;

        var friends = SteamAPI_SteamFriends_v017();
        if (friends == IntPtr.Zero) return false;

        SteamAPI_ISteamFriends_ActivateGameOverlayToWebPage(friends, url, 0);
        return true;
    }
}

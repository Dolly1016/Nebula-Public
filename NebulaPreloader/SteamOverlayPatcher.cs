using BepInEx.Preloader.Core.Patching;

namespace Nebula.Preloader;

/// <summary>
/// Steam クライアント経由ではなく Among Us.exe を直接起動した場合でも Steam Overlay を使えるようにするための patcher。
/// 通常プラグインの BasePlugin.Load() より前に SteamAPI_Init() を呼ぶだけで、アセンブリの書き換えは行わない。
/// </summary>
[PatcherPluginInfo("net.nebula.steamoverlaypatcher", "Steam Overlay Initializer", "1.0.0")]
public sealed class SteamOverlayPatcher : BasePatcher
{
    public override void Initialize()
    {
        try
        {
            Log.LogMessage(SteamOverlay.Init() ? "SteamAPI_Init succeeded." : "SteamAPI_Init returned false.");
        }
        catch (Exception e)
        {
            Log.LogError($"SteamAPI_Init failed: {e}");
        }
    }
}

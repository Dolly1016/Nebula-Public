using Virial.Media;

namespace Virial.Accessibility;

/// <summary>
/// 緊急会議中、画面上部に表示されるオーバーレイを展開するアイコンを管理します。
/// </summary>
public interface OverlayHolder
{
    /// <summary>
    /// オーバーレイを登録します。
    /// </summary>
    /// <param name="overlay">オーバーレイのGUIウィジェット。</param>
    /// <param name="icon">アイコン。</param>
    /// <param name="color">アイコンの色。</param>
    void RegisterOverlay(GUIWidgetSupplier overlay, Image icon, Color color);
}

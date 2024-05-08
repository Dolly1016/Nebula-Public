using Virial.Media;

namespace Virial.Accessibility;

public interface OverlayHolder
{
    void RegisterOverlay(GUIWidgetSupplier overlay, Image icon, Color color);
}

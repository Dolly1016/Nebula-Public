using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Virial.Accessibility;

public interface OverlayHolder
{
    void RegisterOverlay(GUIWidgetSupplier overlay, Image icon, Color color);
}

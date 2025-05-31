using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Minimap;

/// <summary>
/// アドミンマップを開いたときに呼び出されます。
/// </summary>
public class MapOpenAdminEvent : AbstractMapOpenEvent
{
    internal MapOpenAdminEvent() : base() { }
}

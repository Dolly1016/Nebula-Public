using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Minimap;

/// <summary>
/// サボタージュマップを開いたときに呼び出されます。
/// </summary>
public class MapOpenSabotageEvent : AbstractMapOpenEvent
{
    internal MapOpenSabotageEvent() : base() { }
}

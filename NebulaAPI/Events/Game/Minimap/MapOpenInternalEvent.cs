using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Minimap;

internal class MapOpenInternalEvent : Event
{
    internal MapBehaviour map;

    internal MapOpenInternalEvent(MapBehaviour map)
    {
        this.map = map;
    }
}

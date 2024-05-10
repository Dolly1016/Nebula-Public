using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

public class LightRangeUpdateEvent : Event
{
    public float LightRange { get => lightRange; set => lightRange = MathF.Max(0f, value); }
    private float lightRange;
    internal LightRangeUpdateEvent(float lightRange)
    {
        LightRange = lightRange;
    }
}

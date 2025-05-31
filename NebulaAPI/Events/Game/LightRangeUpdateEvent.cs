using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// 視界の広さを更新する際に発火します。
/// </summary>
public class LightRangeUpdateEvent : Event
{
    public float LightRange { get => lightRange; set => lightRange = MathF.Max(0f, value); }
    private float lightRange;
    internal LightRangeUpdateEvent(float lightRange)
    {
        LightRange = lightRange;
    }
}

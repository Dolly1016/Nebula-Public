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
    /// <summary>
    /// 徐々に反映される視界の倍率を指定します。停電サボタージュと同じ速さで適用されます。
    /// </summary>
    public float LightRange { get => lightRange; set => lightRange = MathF.Max(0f, value); }
    private float lightRange;

    public float LightSpeed { get => lightSpeed; set => lightSpeed = MathF.Max(1f, value); }
    private float lightSpeed = 1f;
    /// <summary>
    /// 即座に反映される視界の倍率を指定します。
    /// </summary>
    public float LightQuickRange { get => lightQuickRange; set => lightQuickRange = MathF.Max(0f, value); }
    private float lightQuickRange;
    internal LightRangeUpdateEvent(float lightRange)
    {
        LightRange = lightRange;
        LightQuickRange = 1f;
    }
}
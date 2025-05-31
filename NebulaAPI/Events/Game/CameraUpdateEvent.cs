using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// カメラが更新される際に呼び出されます。
/// カメラの色調を変更できます。
/// </summary>
public class CameraUpdateEvent : Event
{
    private float saturation = 1f;
    public void UpdateSaturation(float value, bool multiply)
    {
        if (multiply)
            saturation *= value;
        else
            saturation += value;
    }

    private float hue = 0f;
    public void UpdateHue(float value)
    {
            hue += value;
    }

    private float brightness = 1f;
    public void UpdateBrightness(float value, bool multiply)
    {
        if (multiply)
            brightness *= value;
        else
            brightness += value;
    }

    private UnityEngine.Color color = UnityEngine.Color.white;
    internal UnityEngine.Color Color { get =>  color; set => color = value; }

    internal float GetSaturation() { return saturation; }
    internal float GetHue() { return hue; }
    internal float GetBrightness() { return brightness; }

    internal CameraUpdateEvent() { }
}

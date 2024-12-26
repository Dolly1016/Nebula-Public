using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

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

    internal float GetSaturation() { return saturation; }
    internal float GetHue() { return hue; }

    internal CameraUpdateEvent() { }
}

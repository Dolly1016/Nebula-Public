using Virial.Utilities;

namespace Virial;

public struct Color
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; }

    public Color(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public Color(byte r,byte g, byte b, byte a = 255)
    {
        R = (float)r / 255f;
        G = (float)g / 255f;
        B = (float)b / 255f;
        A = (float)a / 255f;
    }

    internal Color(UnityEngine.Color color)
    {
        R = color.r;
        G = color.g;
        B = color.b;
        A = color.a;
    }

    internal UnityEngine.Color ToUnityColor() => new UnityEngine.Color(R, G, B, A);

    static public Color ImpostorColor { get; internal set; } = new(global::Palette.ImpostorRed);
    static public Color CrewmateColor { get; internal set; } = new(global::Palette.CrewmateBlue);
    static public Color Red { get; internal set; } = new(1f,0f,0f,1f);
    static public Color Yellow { get; internal set; } = new(1f, 1f, 0f, 1f);
    static public Color Green { get; internal set; } = new(0f, 1f, 0f, 1f);
    static public Color Cyan { get; internal set; } = new(0f, 1f, 1f, 1f);
    static public Color Blue { get; internal set; } = new(0f, 0f, 1f, 1f);
    static public Color Magenta { get; internal set; } = new(1f, 0f, 1f, 1f);
    static public Color White { get; internal set; } = new(1f, 1f, 1f, 1f);
    static public Color Black { get; internal set; } = new(0f, 0f, 0f, 1f);
    static public Color Gray { get; internal set; } = new(0.5f, 0.5f, 0.5f, 1f);
    static public Color Clear { get; internal set; } = new(0f, 0f, 0f, 0f);

    public static Color Lerp(Color from, Color to, float t)
    {
        t = Mathn.Clamp01(t);
        return new Color(from.R + (to.R - from.R) * t, from.G + (to.G - from.G) * t, from.B + (to.B - from.B) * t, from.A + (to.A - from.A) * t);
    }

    public static Color LerpUnclamped(Color from, Color to, float t) => new(from.R + (to.R - from.R) * t, from.G + (to.G - from.G) * t, from.B + (to.B - from.B) * t, from.A + (to.A - from.A) * t);

    public Color AlphaMultiplied(float multiplier) => new(R, G, B, A * multiplier);
    public Color RGBMultiplied(float multiplier) => new(R * multiplier, G * multiplier, B * multiplier, A);
    public static Color operator *(Color color, float multiplier) => new(color.R * multiplier, color.G * multiplier, color.B * multiplier, color.A * multiplier);
    public static Color operator *(Color color1, Color color2) => new(color1.R * color2.R, color1.G * color2.G, color1.B * color2.B, color1.A * color2.A);

    public static Color operator +(Color color1, Color color2) => new(color1.R + color2.R, color1.G + color2.G, color1.B + color2.B, color1.A + color2.A);
    public static Color operator -(Color color1, Color color2) => new(color1.R - color2.R, color1.G - color2.G, color1.B - color2.B, color1.A - color2.A);
}


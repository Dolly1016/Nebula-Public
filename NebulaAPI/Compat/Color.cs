﻿using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial;

public struct Color
{
    public float R, G, B, A;

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
    static public Color ImpostorColor { get; internal set; } = new(global::Palette.ImpostorRed);
    static public Color CrewmateColor { get; internal set; } = new(global::Palette.CrewmateBlue);
    static public Color Red { get; internal set; } = new(1f,0f,0f,1f);
    static public Color Green { get; internal set; } = new(0f, 1f, 0f, 1f);
    static public Color Blue { get; internal set; } = new(0f, 0f, 1f, 1f);
    static public Color White { get; internal set; } = new(1f, 1f, 1f, 1f);
    static public Color Black { get; internal set; } = new(0f, 0f, 0f, 1f);
}


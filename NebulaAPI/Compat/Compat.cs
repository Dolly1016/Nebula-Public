﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Compat;

public struct Size
{
    public float Width;
    public float Height;

    public Size(float width, float height)
    {
        Width = width; Height = height;
    }

    internal Size(UnityEngine.Vector2 size)
    {
        Width = size.x;
        Height = size.y;
    }

    internal UnityEngine.Vector2 ToUnityVector() {
        return new(Width, Height);
    }

}

public struct FuzzySize
{
    public float? Width;
    public float? Height;

    public FuzzySize(float? width, float? height)
    {
        Width = width; Height = height;
        if (!Width.HasValue && !Height.HasValue) Width = 1f;
    }
}

public struct Vector2
{
    public float x , y;

    public Vector2()
    {
        this.x = 0f;
        this.y = 0f;
    }

    public Vector2(float x,float y)
    {
        this.x = x;
        this.y = y;
    }
}


public struct Vector3
{
    public float x, y, z;

    public Vector3()
    {
        this.x = 0f;
        this.y = 0f;
        this.z = 0f;
    }

    public Vector3(float x, float y, float z = 0f)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    internal UnityEngine.Vector3 ToUnityVector()
    {
        return new UnityEngine.Vector3(x, y, z);
    }
}
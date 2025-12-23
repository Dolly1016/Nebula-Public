using System.Runtime.CompilerServices;

namespace Nebula.Utilities;

public static class MathHelpers
{
    /// <summary>
    /// Zの値を無視した距離を計算します。
    /// </summary>
    /// <param name="myVec"></param>
    /// <param name="vector"></param>
    /// <returns></returns>
    static public float Distance(this Vector3 myVec, Vector3 vector) {
        var vec = myVec - vector;
        vec.z = 0;
        return vec.magnitude;
    }

    static public float Distance(this Vector2 myVec, Vector2 vector) => (myVec - vector).magnitude;
}

public static class Mathn
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int Clamp(int val, int min, int max)
    {
        if (val < min) return min;
        if (val > max) return max;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Clamp(float val, float min, float max)
    {
        if (val < min) return min;
        if (val > max) return max;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Clamp01(float val)
    {
        if (val < 0f) return 0f;
        if (val > 1f) return 1f;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Abs(float val)
    {
        if (val < 0f) return -val;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Saturate(float val)
    {
        if (val < 0f) return 0f;
        if (val > 1f) return 1f;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float SmoothStep(float from, float to, float t)
    {
        t = t * t * (3 - 2 * t);
        return to * t + from * (1.0f - t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Min(float v1, float v2)
    {
        return v1 < v2 ? v1 : v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Max(float v1, float v2)
    {
        return v1 > v2 ? v1 : v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Min(float v1, float v2, float v3)
    {
        return v1 < v2 ? (v1 < v3) ? v1 : v3 : (v2 < v3) ? v2 : v3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Max(float v1, float v2, float v3)
    {
        return v1 > v2 ? (v1 > v3) ? v1 : v3 : (v2 > v3) ? v2 : v3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int Min(int v1, int v2)
    {
        return v1 < v2 ? v1 : v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int Max(int v1, int v2)
    {
        return v1 > v2 ? v1 : v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int Min(int v1, int v2, int v3)
    {
        return v1 < v2 ? (v1 < v3) ? v1 : v3 : (v2 < v3) ? v2 : v3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int Max(int v1, int v2, int v3)
    {
        return v1 > v2 ? (v1 > v3) ? v1 : v3 : (v2 > v3) ? v2 : v3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Ceil(float f)
    {
        return MathF.Ceiling(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int CeilToInt(float f)
    {
        return (int)MathF.Ceiling(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Atan2(float y, float x) => MathF.Atan2(y, x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Sin(float v) => MathF.Sin(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Cos(float v) => MathF.Cos(v);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Exp(float v) => MathF.Exp(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Lerp(float from, float to, float v)
    {
        v = Clamp01(v);
        return to * v + from * (1f - v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float LerpUnclamped(float from, float to, float v)
    {
        return to * v + from * (1f - v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Log(float f, float p) => MathF.Log(f, p);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float Pow(float x, float y) => MathF.Pow(x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public int Sign(float v) => MathF.Sign(v);

    public const float PI = (float)Math.PI;
    public const float PI2 = (float)(Math.PI * 2.0);

}

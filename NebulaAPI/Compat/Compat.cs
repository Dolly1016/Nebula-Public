using MS.Internal.Xml.XPath;
using System.Runtime.CompilerServices;
using Virial.Utilities;

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

    internal Size(Virial.Compat.Vector2 size)
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

    public static readonly Vector2 Zero = new(0f, 0f);
    public static readonly Vector2 One = new(1f, 1f);
    public static readonly Vector2 Up = new(0f, 1f);
    public static readonly Vector2 Down = new(0f, -1f);
    public static readonly Vector2 Right = new(1f, 0f);
    public static readonly Vector2 Left = new(-1f, 0f);

    public const float Epsilon = 1e-6f;

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

    internal Vector2(UnityEngine.Vector2 v)
    {
        this.x = v.x;
        this.y = v.y;
    }

    internal UnityEngine.Vector2 ToUnityVector() => new(x, y);

    public Vector3 AsVector3(float z = 0f) => new(x, y, z);
    public UnityEngine.Vector3 AsUnityVector3(float z = 0f) => new(x, y, z);

    public float Magnitude
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MathF.Sqrt(SqrMagnitude);
    }

    public float SqrMagnitude
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => x * x + y * y;
    }

    public Vector2 Normalized => Normalize(this);

    static public implicit operator UnityEngine.Vector2(Vector2 v) => v.ToUnityVector();
    static public implicit operator Vector2(UnityEngine.Vector2 v) => new(v);
    static public implicit operator Vector2(UnityEngine.Vector3 v) => new(v);
    static public Compat.Vector2 operator +(Vector2 v) => v;
    static public Compat.Vector2 operator -(Vector2 v) => new(-v.x, -v.y);
    static public Compat.Vector2 operator +(Vector2 v1, Vector2 v2) => new(v1.x + v2.x, v1.y + v2.y);
    static public Compat.Vector2 operator -(Vector2 v1, Vector2 v2) => new(v1.x - v2.x, v1.y - v2.y);
    static public Compat.Vector2 operator *(Vector2 v1, Vector2 v2) => new(v1.x * v2.x, v1.y * v2.y);
    static public Compat.Vector2 operator /(Vector2 v1, Vector2 v2) => new(v1.x / v2.x, v1.y / v2.y);
    static public Compat.Vector2 operator *(Vector2 v, float a) => new(v.x * a, v.y * a);
    static public Compat.Vector2 operator /(Vector2 v, float a) => new(v.x / a, v.y / a);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Distance(Vector2 v) => MathF.Sqrt(SquaredDistance(v));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float Distance(UnityEngine.Vector2 v) => MathF.Sqrt(SquaredDistance(v));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float SquaredDistance(Vector2 v) => (v.x - x) * (v.x - x) + (v.y - y) * (v.y - y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Rotate(float degrees) => RotateDeg(this, degrees);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 WithX(float newX) => new(newX, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    public Vector2 WithY(float newY) => new(x, newY);

    // Static methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;

    // 2D cross product の z 成分
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Normalize(Vector2 v)
    {
        float sqr = v.x * v.x + v.y * v.y;
        if (sqr <= Epsilon * Epsilon)
            return Zero;

        float inv = 1f / MathF.Sqrt(sqr);
        return new Vector2(v.x * inv, v.y * inv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MagnitudeOf(Vector2 v) => MathF.Sqrt(v.x * v.x + v.y * v.y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SqrMagnitudeOf(Vector2 v) => v.x * v.x + v.y * v.y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt(SqrDistance(a, b));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SqrDistance(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        t = Mathn.Clamp01(t);
        return LerpUnclamped(a, b, t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t)
    {
        return new Vector2(
            a.x + (b.x - a.x) * t,
            a.y + (b.y - a.y) * t
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
    {
        float dx = target.x - current.x;
        float dy = target.y - current.y;
        float sqr = dx * dx + dy * dy;

        if (sqr == 0f || sqr <= maxDistanceDelta * maxDistanceDelta)
            return target;

        float dist = Mathn.Sqrt(sqr);
        return new Vector2(
            current.x + dx / dist * maxDistanceDelta,
            current.y + dy / dist * maxDistanceDelta
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ClampMagnitude(Vector2 v, float maxLength)
    {
        float sqr = v.SqrMagnitude;
        float maxSqr = maxLength * maxLength;

        if (sqr <= maxSqr)
            return v;

        float inv = maxLength / Mathn.Sqrt(sqr);
        return new Vector2(v.x * inv, v.y * inv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Project(Vector2 vector, Vector2 onNormal)
    {
        float sqr = onNormal.SqrMagnitude;
        if (sqr <= Epsilon)
            return Zero;

        float scale = Dot(vector, onNormal) / sqr;
        return new Vector2(onNormal.x * scale, onNormal.y * scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Reflect(Vector2 inDirection, Vector2 inNormal)
    {
        float d = Dot(inDirection, inNormal);
        return new Vector2(
            inDirection.x - 2f * d * inNormal.x,
            inDirection.y - 2f * d * inNormal.y
        );
    }

    // Unity.Vector2.Perpendicular と同じ向き: (-y, x)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Perpendicular(Vector2 v) => new(-v.y, v.x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateDeg(Vector2 v, float degrees) => RotateRad(v, degrees.DegToRad());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateRad(Vector2 v, float radians)
    {
        float s = Mathn.Sin(radians);
        float c = Mathn.Cos(radians);

        return new Vector2(
            v.x * c - v.y * s,
            v.x * s + v.y * c
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Angle(Vector2 from, Vector2 to)
    {
        float denom = MathF.Sqrt(from.SqrMagnitude * to.SqrMagnitude);
        if (denom <= Epsilon)
            return 0f;

        float cos = Mathn.Clamp(Dot(from, to) / denom, -1f, 1f);
        return MathF.Acos(cos).RadToDeg();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SignedAngle(Vector2 from, Vector2 to)
    {
        float unsigned = Angle(from, to);
        float sign = Cross(from, to) < 0f ? -1f : 1f;
        return unsigned * sign;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Min(Vector2 a, Vector2 b) => new(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Max(Vector2 a, Vector2 b) => new(MathF.Max(a.x, b.x), MathF.Max(a.y, b.y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approximately(Vector2 a, Vector2 b, float epsilon = 0.0001f)
    {
        return MathF.Abs(a.x - b.x) <= epsilon
            && MathF.Abs(a.y - b.y) <= epsilon;
    }
}


public struct Vector3
{
    public float x, y, z;

    public static readonly Vector3 Zero = new(0f, 0f, 0f);
    public static readonly Vector3 One = new(1f, 1f, 1f);

    public const float Epsilon = 1e-6f;

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

    public Vector3(UnityEngine.Vector3 v)
    {
        this.x = v.x;
        this.y = v.y;
        this.z = v.z;
    }

    public Vector2 AsVector2() => new(x, y);

    internal UnityEngine.Vector3 ToUnityVector() => new UnityEngine.Vector3(x, y, z);

    static public implicit operator UnityEngine.Vector3(Vector3 v) => v.ToUnityVector();
    static public implicit operator Vector3(UnityEngine.Vector2 v) => new(v);
    static public implicit operator Vector3(UnityEngine.Vector3 v) => new(v);
    public Vector3 Normalized => Normalize(this);
    static public Compat.Vector3 operator +(Vector3 v) => v;
    static public Compat.Vector3 operator -(Vector3 v) => new(-v.x, -v.y, -v.z);
    static public Compat.Vector3 operator +(Vector3 v1, Vector3 v2) => new(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
    static public Compat.Vector3 operator -(Vector3 v1, Vector3 v2) => new(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
    static public Compat.Vector3 operator *(Vector3 v, float a) => new(v.x * a, v.y * a, v.z * a);
    static public Compat.Vector3 operator /(Vector3 v, float a) => new(v.x / a, v.y / a, v.z / a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Normalize(Vector3 v)
    {
        float sqr = v.x * v.x + v.y * v.y + v.z * v.z;
        if (sqr <= Epsilon * Epsilon)
            return Zero;

        float inv = 1f / MathF.Sqrt(sqr);
        return new Vector3(v.x * inv, v.y * inv, v.z * inv);
    }

}

public struct Vector4
{
    public float x, y, z, w;

    public Vector4()
    {
        this.x = 0f;
        this.y = 0f;
        this.z = 0f;
        this.w = 0f;
    }

    public Vector4(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public Vector4(UnityEngine.Vector4 v)
    {
        this.x = v.x;
        this.y = v.y;
        this.z = v.z;
        this.w = v.w;
    }

    internal UnityEngine.Vector4 ToUnityVector() => new UnityEngine.Vector4(x, y, z, w);

    static public implicit operator UnityEngine.Vector4(Vector4 v) => v.ToUnityVector();
    static public implicit operator Vector4(UnityEngine.Vector4 v) => new(v);
    static public Compat.Vector4 operator +(Vector4 v) => v;
    static public Compat.Vector4 operator -(Vector4 v) => new(-v.x, -v.y, -v.z, -v.w);
    static public Compat.Vector4 operator +(Vector4 v1, Vector4 v2) => new(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w + v2.w);
    static public Compat.Vector4 operator -(Vector4 v1, Vector4 v2) => new(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z, v1.w - v2.w);
    static public Compat.Vector4 operator *(Vector4 v, float a) => new(v.x * a, v.y * a, v.z * a, v.x * a);
    static public Compat.Vector4 operator /(Vector4 v, float a) => new(v.x / a, v.y / a, v.z / a, v.x / a);
}
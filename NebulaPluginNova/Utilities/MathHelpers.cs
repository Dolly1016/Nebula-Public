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

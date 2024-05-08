namespace Nebula.Compat;

public static class APICompat
{
    static public UnityEngine.Color ToUnityColor(this Virial.Color color) => new UnityEngine.Color(color.R,color.G,color.B,color.A);
}

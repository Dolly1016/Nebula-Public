using Steamworks;
using System.Text;

namespace Nebula.Utilities;

static public class StringHelper
{
    private static byte ToByte(float f)
    {
        f = Mathf.Clamp01(f);
        return (byte)(f * 255);
    }

    public static string Bold(this string original)
    {
        return "<b>" + original + "</b>";
    }

    public static string Italic(this string original)
    {
        return "<i>" + original + "</i>";
    }

    public static string Sized(this string original, int percentage)
    {
        return $"<size={percentage}%>" + original + "</size>";
    }

    public static string Color(this string original,Color color)
    {
        return string.Format("<color=#{0:X2}{1:X2}{2:X2}{3:X2}>{4}</color>", ToByte(color.r), ToByte(color.g), ToByte(color.b), ToByte(color.a), original);
    }

    public static string ColorBegin(Color color) => string.Format("<color=#{0:X2}{1:X2}{2:X2}{3:X2}>", ToByte(color.r), ToByte(color.g), ToByte(color.b), ToByte(color.a));
    public static string ColorEnd() => "</color>";

    public static string HeadLower(this string text) => char.ToLower(text[0]) + text.Substring(1);
    public static string HeadUpper(this string text) => char.ToUpper(text[0]) + text.Substring(1);

    public static string ToByteString(this string text)
    {
        return text.ComputeConstantHashAsStringLong();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="separator">最初の区切りで1が与えられる。区切りの位置ごとに別の区切り文字を使える。</param>
    /// <param name="enumerable"></param>
    /// <returns></returns>
    public static string Join(Func<int, string> separator, IEnumerable<string> enumerable)
    {
        int num = 0;
        StringBuilder builder = new();
        foreach(var t in enumerable)
        {
            if (num > 0) builder.Append(separator.Invoke(num));
            builder.Append(t);
            num++;
        }
        return builder.ToString();
    }
}

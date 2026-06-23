using Il2CppSystem.Text.Json;
using Nebula.Modules.Cosmetics;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Utilities;

public static class ColorHelper
{
    
    /// <summary>
    /// HSV色空間に変換します。
    /// </summary>
    /// <param name="color"></param>
    /// <param name="h">0～360の範囲で色相を求めます</param>
    /// <param name="s">0～1の範囲で彩度を求めます</param>
    /// <param name="v">0～1の範囲で明度を求めます</param>
    static public void ToHSV(this VColor color, out float h, out float s, out float v)
    {
        float max = Mathn.Max(color.R, color.G, color.B);
        float min = Mathn.Min(color.R, color.G, color.B);

        if (max == min)
            h = 0f;
        else if (max == color.R)
            h = 60f * ((color.G - color.B) / (max - min));
        else if (max == color.G)
            h = 60f * ((color.B - color.R) / (max - min)) + 120f;
        else if (max == color.B)
            h = 60f * ((color.R - color.G) / (max - min)) + 240f;
        else
            h = 0f;//Error

        if (h < 0f) h += 360f;
        if (h > 360f) h -= 360f;

        s = (max - min) / max;
        v = max;
    }

    static public bool IsPink(VColor color)
    {
        color.ToHSV(out var h, out var s, out var v);
        return 309f < h && h < 340f && v > 0.6f;
    }

    static public bool IsLightGreen(VColor color)
    {
        color.ToHSV(out var h, out var s, out var v);
        return 68f < h && h < 147f && v > 0.75f;
    }

    static public bool IsGreenOrBlack(VColor color)
    {
        color.ToHSV(out var h, out var s, out var v);
        return (98f < h && h < 146f && v < 0.8f && s > 0.56f) || (v < 0.22f && s < 0.08f);
    }

    static public bool IsVividColor(VColor color)
    {
        color.ToHSV(out _, out var s, out _);
        return s > 0.85f;
    }

    static public float GetLuminance(VColor color)
    {
        return color.R * 0.299f + color.G * 0.587f + color.B * 0.114f;
    }

    public static bool IsLightColor(VColor color)
    {
        var max = Mathn.Max(color.R, color.G, color.B);
        var sum = color.R + color.G + color.B;
        return max > 0.8f || sum > 2.1f;
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class BalancedColorManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static BalancedColorManager() => DIManager.Instance.RegisterModule(() => new BalancedColorManager());

    private BalancedColorManager()
    {
        ModSingleton<BalancedColorManager>.Instance = this;
        this.RegisterPermanently();
    }

    float middleLuminance = 0.5f;
    public float MiddleLuminance => middleLuminance;
    public bool IsLightColor(VColor color) => ColorHelper.GetLuminance(color) > middleLuminance;
    void OnGameStarted(GameStartEvent _)
    {
        if (GamePlayer.AllPlayers.Count() == 1)
        {
            middleLuminance = ColorHelper.IsLightColor(DynamicPalette.PlayerColors[GamePlayer.LocalPlayer!.PlayerId]) ? 0f : 1f;
        }
        else
        {
            middleLuminance = GetMedian(GamePlayer.AllPlayers.Select(p => ColorHelper.GetLuminance(DynamicPalette.PlayerColors[p.PlayerId])));
        }
    }

    private static float GetMedian(IEnumerable<float> numbers)
    {
        var sortedNumbers = numbers.OrderBy(n => n).ToArray();
        int n = sortedNumbers.Length;

        if (n == 0) return 0f;
        
        // 4. 中央のインデックスを計算
        int midIndex = n / 2;

        if (n % 2 == 1)
        {
            return sortedNumbers[midIndex];
        }
        else
        {
            return (sortedNumbers[midIndex - 1] + sortedNumbers[midIndex]) / 2f;
        }
    }
}

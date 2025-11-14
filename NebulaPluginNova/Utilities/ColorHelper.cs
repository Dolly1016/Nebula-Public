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
    static public void ToHSV(this Color color, out float h, out float s, out float v)
    {
        float max = Mathf.Max(color.r, color.g, color.b);
        float min = Mathf.Min(color.r, color.g, color.b);

        if (max == min)
            h = 0f;
        else if (max == color.r)
            h = 60f * ((color.g - color.b) / (max - min));
        else if (max == color.g)
            h = 60f * ((color.b - color.r) / (max - min)) + 120f;
        else if (max == color.b)
            h = 60f * ((color.r - color.g) / (max - min)) + 240f;
        else
            h = 0f;//Error

        if (h < 0f) h += 360f;
        if (h > 360f) h -= 360f;

        s = (max - min) / max;
        v = max;
    }

    static public bool IsPink(Color color)
    {
        color.ToHSV(out var h, out var s, out var v);
        return 309f < h && h < 340f && v > 0.6f;
    }

    static public bool IsLightGreen(Color color)
    {
        color.ToHSV(out var h, out var s, out var v);
        return 68f < h && h < 147f && v > 0.75f;
    }

    static public bool IsGreenOrBlack(Color color)
    {
        color.ToHSV(out var h, out var s, out var v);
        return (98f < h && h < 146f && v < 0.8f && s > 0.56f) || (v < 0.22f && s < 0.08f);
    }

    static public bool IsVividColor(Color color)
    {
        color.ToHSV(out _, out var s, out _);
        return s > 0.85f;
    }

    static public float GetLuminance(Color color)
    {
        return color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
    }

    public static bool IsLightColor(Color color)
    {
        var max = Mathn.Max(color.r, color.g, color.b);
        var sum = color.r + color.g + color.b;
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
    public bool IsLightColor(Color color) => ColorHelper.GetLuminance(color) > middleLuminance;
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

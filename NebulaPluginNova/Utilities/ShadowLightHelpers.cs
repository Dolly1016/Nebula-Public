using Nebula.Behavior;

namespace Nebula.Utilities;

/// <summary>
/// 発射点から伸びるレーザーの画像の種類です。
/// いずれも左端(発射点)の上下は1/4円で、左端中央をpivotとします。
/// </summary>
public enum LaserStyle
{
    /// <summary>
    /// 9分割をTiledで描画します。中央の列・行が繰り返され、右端の長い列でぼんやりフェードアウトします。
    /// </summary>
    Tiled,
    /// <summary>
    /// 6分割(右列なし)をSlicedで描画します。中央列が引き伸ばされ、遠くに行くほど徐々に暗くなります。
    /// </summary>
    Sliced,
    /// <summary>
    /// Tiledから中の行を除いた6分割です。太さを変えても左端は常に半円になります。
    /// </summary>
    TiledRound,
    /// <summary>
    /// Slicedから中の行を除いた4分割です。太さを変えても左端は常に半円になります。
    /// </summary>
    SlicedRound,
}

/// <summary>
/// 影を打ち消す光源と、影の中でも見える物体(影)を生成するヘルパです。
/// </summary>
/// <remarks>
/// 光源の色・透明度はrenderer.colorではなくmaterial.colorで指定してください。
/// </remarks>
public static class ShadowLightHelpers
{
    /// <summary>
    /// 円形の光源。等倍で直径4。縁は広くぼやける。
    /// </summary>
    public static readonly Image LightMaskImage = SpriteLoader.FromResource("Nebula.Resources.LightMask.png", 100f);
    /// <summary>
    /// 円形の光源。等倍で直径2。LightMaskよりも縁がはっきりしている。
    /// </summary>
    public static readonly Image LightSharpMaskImage = SpriteLoader.FromResource("Nebula.Resources.LightSharpMask.png", 100f);
    /// <summary>
    /// 矩形の光源(9分割, 各辺の枠100px)。Slicedで任意の大きさに引き伸ばして使う。
    /// </summary>
    public static readonly Image LightQuadMaskImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LightQuadMask.png", 100f, 100, 100);
    /// <summary>
    /// 両端が丸い線分状の光源(9分割, 左右の枠80px, 上下の枠2px)。始端と終端の間に引き伸ばして使う。
    /// </summary>
    public static readonly Image LaserLightImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LaserLight.png", 100f, 80, 2);

    /// <summary>
    /// 9分割(列 32:32:256px, 行 32:32:32px)。<see cref="LaserStyle.Tiled"/>で使用する。
    /// </summary>
    public static readonly Image LaserTileImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LaserTile.png", 100f, new(32f, 32f, 256f, 32f)).SetPivot(new(0f, 0.5f));
    /// <summary>
    /// 6分割(列 32:256px, 行 32:32:32px)。<see cref="LaserStyle.Sliced"/>で使用する。
    /// </summary>
    public static readonly Image LaserSlicedImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LaserSliced.png", 100f, new(32f, 32f, 0f, 32f)).SetPivot(new(0f, 0.5f));
    /// <summary>
    /// 6分割(列 32:32:256px, 行 32:32px)。<see cref="LaserStyle.TiledRound"/>で使用する。
    /// </summary>
    public static readonly Image LaserTileRoundImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LaserTileRound.png", 100f, new(32f, 32f, 256f, 32f)).SetPivot(new(0f, 0.5f));
    /// <summary>
    /// 4分割(列 32:256px, 行 32:32px)。<see cref="LaserStyle.SlicedRound"/>で使用する。
    /// </summary>
    public static readonly Image LaserSlicedRoundImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LaserSlicedRound.png", 100f, new(32f, 32f, 0f, 32f)).SetPivot(new(0f, 0.5f));

    /// <summary>
    /// <see cref="GenerateSegmentLight"/>で生成する光源の既定の太さ。
    /// </summary>
    public const float DefaultSegmentThickness = 0.95f;
    /// <summary>
    /// <see cref="GenerateSegmentLight"/>で生成する光源が始端・終端からはみ出す既定の長さ。
    /// </summary>
    public const float DefaultSegmentExtension = 0.55f;

    // ---------------- 汎用 ----------------

    /// <summary>
    /// 任意のスプライトで光源を生成します。
    /// </summary>
    /// <param name="drawSize">指定した場合、このサイズでdrawModeに従って描画します。スプライトはFullRectで生成されている必要があります。</param>
    public static SpriteRenderer GenerateLight(Vector2 position, Sprite sprite, Vector2? drawSize = null, SpriteDrawMode drawMode = SpriteDrawMode.Tiled)
        => AmongUsUtil.GenerateCustomLight(position, sprite, drawSize, drawMode);

    /// <summary>
    /// 任意のスプライトで影の中でも見える物体を生成します。
    /// </summary>
    /// <param name="drawSize">指定した場合、このサイズでdrawModeに従って描画します。スプライトはFullRectで生成されている必要があります。</param>
    internal static CustomShadow GenerateShadow(Vector2 position, Sprite sprite, Color? shadowColor = null, Vector2? drawSize = null, SpriteDrawMode drawMode = SpriteDrawMode.Tiled)
        => AmongUsUtil.GenerateCustomShadow(position, sprite, shadowColor, drawSize, drawMode);

    // ---------------- 円形 ----------------

    /// <summary>
    /// 円形の光源を生成します。
    /// </summary>
    /// <param name="sharp">trueの場合は縁がはっきりした光源(等倍で直径2)を、falseの場合は縁が広くぼやけた光源(等倍で直径4)を使用します。</param>
    /// <param name="scale">拡大率</param>
    public static SpriteRenderer GenerateCircleLight(Vector2 position, bool sharp = false, float scale = 1f)
    {
        var renderer = GenerateLight(position, (sharp ? LightSharpMaskImage : LightMaskImage).GetSprite());
        if (scale != 1f) renderer.transform.localScale = new(scale, scale, 1f);
        return renderer;
    }

    // ---------------- 矩形 ----------------

    /// <summary>
    /// 矩形の光源を生成します。縁のぼかしの幅は大きさによらず一定です。
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="size">大きさ。縁のぼかしを含めるため、各辺2以上を推奨します。</param>
    /// <param name="angle">回転角(度)</param>
    public static SpriteRenderer GenerateQuadLight(Vector2 center, Vector2 size, float angle = 0f)
    {
        var renderer = GenerateLight(center, LightQuadMaskImage.GetSprite(), size, SpriteDrawMode.Sliced);
        renderer.transform.localEulerAngles = new(0f, 0f, angle);
        return renderer;
    }

    // ---------------- 線分(始端・終端あり) ----------------

    /// <summary>
    /// 始端と終端の間を照らす、両端が丸い線分状の光源を生成します。
    /// </summary>
    /// <param name="thickness">太さ</param>
    /// <param name="extension">始端・終端からはみ出す長さ</param>
    public static SpriteRenderer GenerateSegmentLight(Vector2 start, Vector2 end, float thickness = DefaultSegmentThickness, float extension = DefaultSegmentExtension)
    {
        var renderer = GenerateLight(start, LaserLightImage.GetSprite(), Vector2.one, SpriteDrawMode.Tiled);
        SetSegmentLight(renderer, start, end, thickness, extension);
        return renderer;
    }

    /// <summary>
    /// <see cref="GenerateSegmentLight"/>で生成した光源の始端と終端を更新します。
    /// </summary>
    public static void SetSegmentLight(SpriteRenderer renderer, Vector2 start, Vector2 end, float thickness = DefaultSegmentThickness, float extension = DefaultSegmentExtension)
    {
        var diff = end - start;
        var transform = renderer.transform;
        transform.position = ((start + end) * 0.5f).AsVector3(transform.position.z);
        transform.localEulerAngles = new(0f, 0f, Mathn.Atan2(diff.y, diff.x).RadToDeg());
        renderer.size = new(diff.magnitude + extension * 2f, thickness);
    }

    // ---------------- レーザー(発射点から伸びる) ----------------

    private static Image GetLaserImage(LaserStyle style) => style switch
    {
        LaserStyle.Sliced => LaserSlicedImage,
        LaserStyle.TiledRound => LaserTileRoundImage,
        LaserStyle.SlicedRound => LaserSlicedRoundImage,
        _ => LaserTileImage,
    };
    private static SpriteDrawMode GetLaserDrawMode(LaserStyle style) => style is LaserStyle.Sliced or LaserStyle.SlicedRound ? SpriteDrawMode.Sliced : SpriteDrawMode.Tiled;

    /// <summary>
    /// 画像本来の太さを取得します。
    /// </summary>
    public static float GetLaserDefaultThickness(LaserStyle style)
    {
        var sprite = GetLaserImage(style).GetSprite();
        return sprite.rect.height / sprite.pixelsPerUnit;
    }

    /// <summary>
    /// 太さと長さから、拡大率と描画サイズを求めます。
    /// 中の行がない画像は縦に引き伸ばす部分がないため、また画像本来の太さより細い場合は枠が潰れるため、Transformの等倍拡大で太さを変えます。
    /// 描画サイズは拡大前の座標系で表すため、拡大率で割ります。
    /// </summary>
    private static (float scale, Vector2 size) CalcLaserScaleAndSize(LaserStyle style, float length, float? thickness)
    {
        float defaultThickness = GetLaserDefaultThickness(style);
        float t = thickness ?? defaultThickness;
        bool round = style is LaserStyle.TiledRound or LaserStyle.SlicedRound;
        float scale = round ? t / defaultThickness : Mathn.Min(1f, t / defaultThickness);
        return (scale, new(length / scale, t / scale));
    }

    private static void ApplyLaserTransform(Transform transform, Vector2 origin, float angle, float scale)
    {
        transform.position = origin.AsVector3(transform.position.z);
        transform.localEulerAngles = new(0f, 0f, angle);
        transform.localScale = new(scale, scale, 1f);
    }

    /// <summary>
    /// 発射点から伸びるレーザー状の光源を生成します。
    /// </summary>
    /// <param name="origin">発射点</param>
    /// <param name="angle">発射方向(度)。0で右向き、反時計回り</param>
    /// <param name="length">長さ</param>
    /// <param name="style">画像の種類</param>
    /// <param name="thickness">太さ。nullの場合は画像本来の太さ</param>
    public static SpriteRenderer GenerateLaserLight(Vector2 origin, float angle, float length, LaserStyle style = LaserStyle.Tiled, float? thickness = null)
    {
        var renderer = GenerateLight(origin, GetLaserImage(style).GetSprite(), Vector2.one, GetLaserDrawMode(style));
        SetLaserLight(renderer, origin, angle, length, style, thickness);
        return renderer;
    }

    /// <summary>
    /// <see cref="GenerateLaserLight"/>で生成した光源の発射点・方向・長さ・太さを更新します。styleは生成時と同じものを指定してください。
    /// </summary>
    public static void SetLaserLight(SpriteRenderer renderer, Vector2 origin, float angle, float length, LaserStyle style = LaserStyle.Tiled, float? thickness = null)
    {
        var (scale, size) = CalcLaserScaleAndSize(style, length, thickness);
        ApplyLaserTransform(renderer.transform, origin, angle, scale);
        renderer.size = size;
    }

    /// <summary>
    /// 発射点から伸びる、影の中でも見えるレーザーを生成します。
    /// </summary>
    /// <param name="origin">発射点</param>
    /// <param name="angle">発射方向(度)。0で右向き、反時計回り</param>
    /// <param name="length">長さ</param>
    /// <param name="style">画像の種類</param>
    /// <param name="thickness">太さ。nullの場合は画像本来の太さ</param>
    /// <param name="shadowColor">影の中での色</param>
    internal static CustomShadow GenerateLaserShadow(Vector2 origin, float angle, float length, LaserStyle style = LaserStyle.Tiled, float? thickness = null, Color? shadowColor = null)
    {
        var shadow = GenerateShadow(origin, GetLaserImage(style).GetSprite(), shadowColor, Vector2.one, GetLaserDrawMode(style));
        SetLaserShadow(shadow, origin, angle, length, style, thickness);
        return shadow;
    }

    /// <summary>
    /// <see cref="GenerateLaserShadow"/>で生成した影の発射点・方向・長さ・太さを更新します。styleは生成時と同じものを指定してください。
    /// </summary>
    internal static void SetLaserShadow(CustomShadow shadow, Vector2 origin, float angle, float length, LaserStyle style = LaserStyle.Tiled, float? thickness = null)
    {
        var (scale, size) = CalcLaserScaleAndSize(style, length, thickness);
        ApplyLaserTransform(shadow.transform, origin, angle, scale);
        shadow.SetSize(size);
    }
}

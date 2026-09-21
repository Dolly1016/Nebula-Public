using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using Virial;

namespace Nebula.Http;

/// <summary>
/// ミニマップの画像と、ゲーム内の座標をその画像の上へ移すための値を閲覧画面へ渡す。
/// </summary>
/// <remarks>
/// <para>
/// 画像は <see cref="NebulaAsset.GetMapSprite"/> がカメラで描き起こしたものなので、
/// バンドル由来のテクスチャと違って読み出せる。そのまま PNG にできる。
/// </para>
/// <para>
/// 役職アイコンと同じく、Unity を触れるのはメインスレッドだけなので
/// <see cref="Prepare"/> をサーバー起動時に一度だけ通して控えておく。
/// </para>
/// </remarks>
internal static class MapContent
{
    private static readonly object Gate = new();
    private static readonly Dictionary<byte, byte[]> images = [];
    private static readonly Dictionary<byte, MapView> views = [];

    private static readonly Virial.Logging.ILogger Logger = NebulaAPI.Logging.NebulaLogger("HttpServer");

    public const string PngContentType = "image/png";

    /// <summary>画像の解像度。1 ゲーム内単位あたりのピクセル数。</summary>
    private const float PixelsPerUnit = 100f;

    /// <summary>ミニマップの色。ゲーム内と同じ値。</summary>
    private static readonly Color MinimapColor = VanillaAsset.MapBlue;

    /// <summary>
    /// 全マップの画像を PNG にして控える。<b>メインスレッドから呼ぶこと。</b>
    /// </summary>
    public static void Prepare()
    {
        lock (Gate)
        {
            if (images.Count > 0) return;

            for (byte mapId = 0; mapId < VanillaAsset.MapAsset.Length; mapId++)
            {
                try
                {
                    Capture(mapId);
                }
                catch (Exception e)
                {
                    //一枚失敗しても他のマップは配れるようにする。
                    Logger.Warning($"Failed to capture a minimap. (map {mapId})\n" + e.Message);
                }
            }

            Logger.Message($"Prepared {images.Count} minimap(s).");
        }
    }

    private static void Capture(byte mapId)
    {
        //dlekS のように素材を持たないマップがある。
        var ship = VanillaAsset.MapAsset[mapId];
        if (ship == null) return;

        var sprite = NebulaAsset.GetMapSprite(mapId, int.MaxValue);
        if (sprite == null) return;

        var png = ImageConversion.EncodeToPNG(sprite.texture);
        if (png == null || png.Length == 0) return;

        var center = VanillaAsset.GetMapCenter(mapId);

        images[mapId] = png;
        views[mapId] = new MapView
        {
            MapId = mapId,
            Width = sprite.texture.width,
            Height = sprite.texture.height,
            PixelsPerUnit = PixelsPerUnit,
            Scale = VanillaAsset.GetMapScale(mapId),
            CenterX = center.x,
            CenterY = center.y,
            Color = ToHex(MinimapColor),
        };
    }

    /// <summary>マップ 1 枚の PNG。用意できていなければ false。</summary>
    public static bool TryGetImage(byte mapId, out byte[] png)
    {
        lock (Gate) return images.TryGetValue(mapId, out png!);
    }

    /// <summary>配れるマップの一覧。</summary>
    public static string Manifest
    {
        get
        {
            lock (Gate) return JsonSerializer.Serialize(new List<MapView>(views.Values), SerializerOptions);
        }
    }

    private static string ToHex(Color color)
    {
        static int Channel(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        return $"#{Channel(color.r):X2}{Channel(color.g):X2}{Channel(color.b):X2}";
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// ミニマップ 1 枚ぶんの情報。
    /// </summary>
    /// <remarks>
    /// ゲーム内の座標 (x, y) は、この画像の上では
    /// <c>((x / scale + centerX) * pixelsPerUnit + width / 2,
    ///   height / 2 - (y / scale + centerY) * pixelsPerUnit)</c> の位置になる。
    /// 画像はマップの原点を中心に切り出されており、画面の上下と y 軸の向きが逆なことに注意。
    /// </remarks>
    private sealed class MapView
    {
        public byte MapId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float PixelsPerUnit { get; set; }

        /// <summary>ゲーム内の距離をミニマップの距離にするときの割る数。</summary>
        public float Scale { get; set; }

        public float CenterX { get; set; }
        public float CenterY { get; set; }

        /// <summary>ミニマップを塗る色。</summary>
        public string Color { get; set; } = "";
    }
}

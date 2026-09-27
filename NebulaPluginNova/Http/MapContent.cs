using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using Virial;

namespace Nebula.Http;

internal static class MapContent
{
    private static readonly object Gate = new();
    private static readonly Dictionary<byte, byte[]> images = [];
    private static readonly Dictionary<byte, MapView> views = [];

    private static readonly Virial.Logging.ILogger Logger = NebulaAPI.Logging.NebulaLogger("HttpServer");

    public const string PngContentType = "image/png";

    private const float PixelsPerUnit = 100f;

    private const int AllRooms = int.MaxValue;

    private static readonly Color MinimapColor = VanillaAsset.MapBlue;

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
                    //一枚失敗しても他のマップは配れるように
                    Logger.Warning($"Failed to capture a minimap. (map {mapId})\n" + e.Message);
                }
            }

            Logger.Message($"Prepared {images.Count} minimap(s).");
        }
    }

    private static void Capture(byte mapId)
    {
        var ship = VanillaAsset.MapAsset[mapId];
        if (ship == null) return;

        var sprite = NebulaAsset.GetMapSprite(mapId, AllRooms);
        if (sprite == null) return;

        var png = ImageConversion.EncodeToPNG(sprite.texture);
        if (png == null || png.Length == 0) return;

        var center = ship.MapPrefab.HerePoint.transform.parent.localPosition;

        var bounds = sprite.bounds;

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
            OriginX = bounds.min.x,
            OriginY = bounds.max.y,
            Color = ToHex(MinimapColor),
        };
    }

    public static bool TryGetImage(byte mapId, out byte[] png)
    {
        lock (Gate) return images.TryGetValue(mapId, out png!);
    }

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

    private sealed class MapView
    {
        public byte MapId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float PixelsPerUnit { get; set; }

        // ゲーム内の距離をミニマップの距離に変換
        public float Scale { get; set; }

        public float CenterX { get; set; }
        public float CenterY { get; set; }

        // 画像の左上が指すミニマップ座標
        public float OriginX { get; set; }
        public float OriginY { get; set; }

        // ミニマップを塗る色
        public string Color { get; set; } = "";
    }
}

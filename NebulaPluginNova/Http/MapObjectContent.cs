using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Nebula.Game.Statistics;

namespace Nebula.Http;

/// <summary>
/// マップに現れる物の絵と、その性質を配る。
/// </summary>
/// <remarks>
/// 性質は種類ごとにハードコードせず <see cref="MapObjectKinds"/> の宣言をそのまま写す。
/// 閲覧画面はこれを見て描き方を決めるので、種類を足してもこちらを直す必要はない。
/// </remarks>
internal static class MapObjectContent
{
    /// <summary>絵の実体。<c>Resources/Http</c> の外にあるので、アドレスで読む。</summary>
    private const string ImageAddress = "Nebula::MapViewerIcons.png";

    /// <summary>絵 1 つぶんの大きさ(ピクセル)。</summary>
    private const int CellSize = 64;

    private static readonly object Gate = new();
    private static byte[]? image = null;
    private static bool tried = false;
    private static int width = 0;
    private static int height = 0;

    public static bool TryGetImage(out byte[] png)
    {
        lock (Gate)
        {
            if (!tried)
            {
                tried = true;
                if (EmbeddedWebContent.TryReadResource(ImageAddress, out var content))
                {
                    image = content;
                    (width, height) = PngSize(content);
                }
            }

            png = image ?? [];
            return png.Length > 0;
        }
    }

    /// <summary>
    /// PNG の大きさを見出しから読む。
    /// </summary>
    /// <remarks>
    /// 何列並んでいるかは絵の枚数ではなく画像の幅で決まる。絵を足したときに食い違わないよう、
    /// 決め打ちせず画像そのものから読む。
    /// </remarks>
    private static (int width, int height) PngSize(byte[] png)
    {
        //8 バイトの印の後に IHDR が続き、その中身の先頭が幅と高さ。いずれもビッグエンディアン。
        if (png.Length < 24) return (0, 0);

        static int Read(byte[] bytes, int at) =>
            (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];

        return (Read(png, 16), Read(png, 20));
    }

    public static string Manifest
    {
        get
        {
            TryGetImage(out _);
            lock (Gate) return Build();
        }
    }

    private static string Build() => JsonSerializer.Serialize(new Sheet
    {
        CellSize = CellSize,
        Width = width,
        Height = height,
        Kinds = [.. MapObjectKinds.All.Select(Describe)],
    }, SerializerOptions);

    private static KindView Describe(MapObjectKind kind) => new()
    {
        Id = kind.Id,
        Icons = kind.Icons,
        Movable = kind.Has<MapObjectTrait.Movable>(),
        Despawnable = kind.Has<MapObjectTrait.Despawnable>(),
        Linear = kind.Has<MapObjectTrait.LinearMotion>(),
        Oriented = kind.Has<MapObjectTrait.Oriented>(),
        Concealable = kind.Has<MapObjectTrait.Concealable>(),
        FollowsOwner = kind.Has<MapObjectTrait.FollowsOwner>(),
        WithinTurn = kind.Has<MapObjectTrait.WithinTurn>(),
        TurnsPerSecond = kind.Get<MapObjectTrait.Spinning>()?.TurnsPerSecond ?? 0f,
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class Sheet
    {
        public int CellSize { get; set; }

        /// <summary>絵が並んだ画像の大きさ。何列あるかはここから割る。</summary>
        public int Width { get; set; }
        public int Height { get; set; }
        public IReadOnlyList<KindView> Kinds { get; set; } = [];
    }

    private sealed class KindView
    {
        public string Id { get; set; } = "";

        /// <summary>段ごとの絵の番号。左上から順に 0, 1, …。段を持たない種類では1つだけ。</summary>
        public IReadOnlyList<int> Icons { get; set; } = [];

        public bool Movable { get; set; }
        public bool Despawnable { get; set; }
        public bool Linear { get; set; }
        public bool Oriented { get; set; }

        /// <summary>見えている時と見えていない時があるか。</summary>
        public bool Concealable { get; set; }

        /// <summary>持ち主に付いて回るか。</summary>
        public bool FollowsOwner { get; set; }

        /// <summary>ターンを跨がないか。</summary>
        public bool WithinTurn { get; set; }

        /// <summary>見せるとき回す速さ。0 なら回さない。</summary>
        public float TurnsPerSecond { get; set; }
    }
}

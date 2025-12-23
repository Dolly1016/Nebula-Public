using Virial.Game;

namespace Nebula.Map;

public class SkeldData : MapData
{
    override public int Id => 0;
    static private Vector2[] MapPositions = new Vector2[]
    {
        //カフェ
        new(0f, 5.3f), new(-5.2f, 1.2f), new(-0.9f, -3.1f), new(4.6f, 1.2f),
        //ウェポン
        new(10.1f, 3f),new(8.45f, 1.1f),
        //コの字通路/O2
        new(9.6f, -3.4f), new(11.8f, -6.5f),
        //ナビ
        new(16.7f, -4.8f),
        //シールド
        new(9.3f, -10.3f), new(9.5f, -14.1f),
        //コミュ上
        new(5.2f, -12.2f),
        //コミュ
        new(3.8f, -15.4f),
        //ストレージ
        new(-0.3f, -9.8f), new(-0.28f, -16.4f), new(-4.5f, -14.3f),
        //エレク
        new(-9.6f, -11.3f), new(-7.5f, -8.4f),
        //ロアエンジン右
        new(-12.1f, -11.4f),
        //ロアエンジン
        new(-15.4f, -13.1f), new(-16.8f, -9.8f),
        //アッパーエンジン
        new(-16.8f, -1f), new(-15.2f, 2.4f),
        //セキュ
        new(-13.8f, -4.5f),
        //リアクター
        new(-20.9f, -5.4f),
        //メッドベイ
        new(-7.3f, -4.6f), new(-9.2f, -2.1f),
        //アドミン
        new(2.6f, -7.1f), new(6.3f, -9.5f)
    };

    static private Vector2[] NonMapPositions = new Vector2[]
    {
        //ナビ左上
        new(13.55f, -3.5f),
        //リアクター右上方
        new(-18.45f, -2.65f)
    };

    static private (AdditionalRoomArea area, string key, bool detailRoom)[] additionalRooms = [
        (new(-16.54f, -5.24f, 3.38f, 4.26f), "reactorAccess", false),
        (new(-10.58f, 0.93f, 4.69f, 1.94f), "medBayAccess", false),
        (new(-9.73f, -12.97f, 5.34f, 3.71f), "electricalAccess", false),
        (new(0.01f, -6.87f, 2.61f, 2.86f), "adminAccess", false),
        (new(3.97f, -12.66f, 3.58f, 2.21f), "commsAccess", false),
        (new(11.89f, -5.86f, 4.87f, 5.37f), "rightAccess", false),
        (new(5.86f, 1.18f, 1.74f, 1.49f), "weaponsAccess", false),
        ];
    static private (SystemTypes room, AdditionalRoomArea area, string key)[] overrideRooms = [];

    static private MapObjectPoint[] mapObjectPoints = [
        new(0.1f, 5.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //カフェ上
        new(-5.7f, 3.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //カフェ左上
        new(-3.3f, 4.1f, MapObjectType.SmallOrTabletopOutOfSight), //カフェ左上テーブル
        new(2.8f, -2.4f, MapObjectType.SmallInCorner), //カフェ右下
        new(-7.2f, -2.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //メッドベイ右上
        new(-11.4f, 1.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //メッドベイ上通路
        new(-10.3f, -2.8f, MapObjectType.SmallOrTabletopOutOfSight | MapObjectType.DepoisonBox), //メッドベイ左ベッド
        new(-18.4f, 2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //アッパーエンジン上
        new(-16.4f, -4.1f, MapObjectType.SmallInCorner), //十字路上方
        new(-17.3f, -6.9f, MapObjectType.SmallInCorner), //十字路下方
        new(-16.9f, -5.4f, MapObjectType.Reachable), //十字路中央
        new(-22.5f, -6.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //リアクター中央下
        new(-22.4f, -8.0f, MapObjectType.SmallOrTabletopOutOfSight), //リアクター左下
        new(-12.2f, -4.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //セキュ中央
        new(-15.2f, -9.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ロアエンジン上
        new(-17.1f, -13.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ロアエンジン下
        new(-12.5f, -14.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ロア電気室間下
        new(-11.7f, -11.2f, MapObjectType.Reachable), //ロア電気室間上
        new(-7.3f, -11.8f, MapObjectType.SmallInCorner), //電気室下
        new(-8.2f, -8.9f, MapObjectType.SmallOrTabletopOutOfSight), //電気室上側
        new(-3.7f, -11.7f, MapObjectType.SmallInCorner), //ストレージ左上
        new(-0.8f, -14.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ストレージ右下
        new(-2.8f, -16.9f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ストレージ左下
        new(5.3f, -9.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //アドミン右下
        new(9.9f, 0.3f, MapObjectType.SmallInCorner), //ウェポン
        new(9.7f, 3.0f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ウェポン右上
        new(6.7f, -4.7f, MapObjectType.SmallOrTabletopOutOfSight), //O2下寄り
        new(12.2f, -2.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //O2ナビ間
        new(18.1f, -5.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ナビ
        new(17.0f, -6.3f, MapObjectType.SmallOrTabletopOutOfSight), //ナビ下
        new(9.8f, -7.5f, MapObjectType.SmallInCorner), //ナビシールド間
        new(10.1f, -13.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //シールド
        new(3.6f, -11.7f, MapObjectType.SmallInCorner), //コミュ前通路
        new(1.9f, -14.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コミュ
        ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    public override IReadOnlyList<Vector2> MapArea => MapPositions;
    public override IReadOnlyList<Vector2> NonMapArea => NonMapPositions;
    protected override (AdditionalRoomArea area, string key, bool detailRoom)[] AdditionalRooms => additionalRooms;
    protected override (SystemTypes room, AdditionalRoomArea area, string key)[] OverrideRooms => overrideRooms;
    protected override SystemTypes[] SabotageTypes => new SystemTypes[] { SystemTypes.Reactor, SystemTypes.Comms, SystemTypes.Electrical, SystemTypes.LifeSupp };
    internal static IDividedSpriteLoader SealedVentSpriteSkeld = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedVentSkeld.png", 100f, 8, 4);
    protected override IDividedSpriteLoader SealedVentSprite => SealedVentSpriteSkeld;
    private HashSet<string> altSealedVents = ["WeaponsVent", "NavVentNorth", "ElecVent", "UpperReactorVent"];
    public override Sprite GetSealedVentSprite(Vent vent, int level, bool remove)
    {
        return base.GetSealedVentSprite(vent, altSealedVents.Contains(vent.name) ? (8 + level) : level, remove);
    }

    internal static IDividedSpriteLoader SealedDoorSpriteSkeldH = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorSkeldH.png", 100f, 8, 1);
    internal static IDividedSpriteLoader SealedDoorSpriteSkeldV = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorSkeldV.png", 100f, 8, 2);

    public override (SystemTypes room, Vector2 pos)[] AdminRooms { get; } = [
        (SystemTypes.MedBay, new(-8.6f, -2.4f)),
        (SystemTypes.UpperEngine, new(-17.6f,-0.5f)),
        (SystemTypes.Security, new(-13.2f, -4.1f)),
        (SystemTypes.Reactor, new(-20.9f, -5.5f)),
        (SystemTypes.LowerEngine, new(-17.1f, -11.2f)),
        (SystemTypes.Electrical, new(-8.0f,-9.0f)),
        (SystemTypes.Storage, new(-3.0f,-14.1f)),
        (SystemTypes.Admin, new(4.5f, -7.7f)),
        (SystemTypes.Cafeteria, new(-0.5f, 1.0f)),
        (SystemTypes.Weapons, new(9.5f, 1.4f)),
        (SystemTypes.LifeSupp, new(6.5f, -3.5f)),
        (SystemTypes.Nav, new(16.7f, -4.7f)),
        (SystemTypes.Shields, new(9.1f, -12.3f)),
        (SystemTypes.Comms, new(4.6f, -15.6f)),
        ];

    static internal Vector2[][] SkeldShadowEdges = [
        //外周
        [new(17.47f, -2.06f), new(15.55f, -2.01f), new(15.54f, -3.39f), new(12.72f, -3.41f), new(12.73f, -2.08f), new(10.34f, -2.06f), new(10.33f, -0.92f), new(11.71f, -0.91f), new(11.73f, 2.25f), new(9.53f, 4.46f), new(7.02f, 4.44f), new(7.04f, 2.42f), new(4.98f, 2.42f), new(4.98f, 3.90f), new(2.13f, 6.77f), new(-4.30f, 6.79f), new(-6.24f, 4.80f), new(-6.29f, 2.41f), new(-14.79f, 2.41f), new(-14.80f, 3.65f), new(-18.33f, 3.64f), new(-19.59f, 2.62f), new(-19.59f, -1.63f), new(-17.77f, -1.64f), new(-17.76f, -4.06f), new(-19.15f, -4.10f), new(-19.15f, -2.64f), new(-20.75f, -2.62f), new(-20.74f, -0.96f), new(-21.84f, -0.92f), new(-23.60f, -2.13f), new(-23.59f, -7.93f), new(-21.83f, -9.13f), new(-20.73f, -9.09f), new(-20.73f, -7.42f), new(-19.16f, -7.41f), new(-19.14f, -6.13f), new(-17.78f, -6.16f), new(-17.75f, -8.74f), new(-19.58f, -8.79f), new(-19.59f, -13.05f), new(-18.30f, -14.05f), new(-14.81f, -14.05f), new(-14.83f, -12.42f), new(-12.96f, -12.41f), new(-12.94f, -15.25f), new(-5.15f, -15.25f), new(-5.17f, -15.49f), new(-3.07f, -17.57f), new(1.00f, -17.56f), new(1.01f, -12.96f), new(4.30f, -12.95f), new(4.32f, -13.66f), new(1.45f, -13.68f), new(1.47f, -16.52f), new(2.51f, -17.56f), new(5.52f, -17.54f), new(6.62f, -16.53f), new(6.57f, -13.67f), new(5.97f, -13.67f), new(5.95f, -12.96f), new(7.04f, -12.98f), new(7.09f, -15.01f), new(9.50f, -14.99f), new(11.71f, -12.81f), new(11.73f, -9.67f), new(10.33f, -9.67f), new(10.36f, -7.15f), new(12.70f, -7.16f), new(12.72f, -5.44f), new(15.58f, -5.44f), new(15.61f, -6.82f), new(17.48f, -6.85f), new(19.17f, -5.48f), new(19.20f, -3.50f), new(17.47f, -2.05f)],
        //右側
        [new(0.14f, -4.52f), new(0.14f, -5.81f), new(7.03f, -5.85f), new(7.04f, -9.49f), new(6.10f, -10.39f), new(1.98f, -10.40f), new(1.95f, -7.86f), new(0.15f, -7.81f), new(0.15f, -8.29f), new(0.97f, -8.35f), new(1.01f, -10.91f), new(7.04f, -10.94f), new(7.03f, -10.76f), new(8.08f, -9.68f), new(8.65f, -9.67f), new(8.68f, -5.16f), new(11.08f, -5.14f), new(11.09f, -4.11f), new(7.83f, -4.12f), new(7.83f, -5.24f), new(4.04f, -5.20f), new(4.05f, -3.63f), new(5.58f, -2.15f), new(5.59f, -1.68f), new(7.71f, -1.69f), new(7.71f, -2.04f), new(8.62f, -2.06f), new(8.64f, -0.90f), new(8.12f, -0.91f), new(7.05f, 0.17f), new(7.05f, 0.36f), new(5.01f, 0.38f), new(5.02f, -1.91f), new(2.40f, -4.50f), new(0.14f, -4.54f)],
        //左側
        [new(-1.55f, -4.53f), new(-3.77f, -4.53f), new(-6.30f, -2.00f), new(-6.27f, 0.38f), new(-8.32f, 0.37f), new(-8.31f, -0.26f), new(-6.91f, -0.28f), new(-6.90f, -2.81f), new(-5.11f, -4.57f), new(-5.16f, -5.89f), new(-10.37f, -5.87f), new(-11.21f, -4.86f), new(-11.18f, -0.25f), new(-9.99f, -0.26f), new(-10.00f, 0.38f), new(-14.82f, 0.38f), new(-14.82f, -1.65f), new(-16.10f, -1.70f), new(-16.12f, -4.08f), new(-14.55f, -4.05f), new(-14.53f, -2.90f), new(-13.60f, -1.99f), new(-12.60f, -1.96f), new(-11.85f, -2.71f), new(-11.84f, -7.35f), new(-14.57f, -7.40f), new(-14.54f, -6.13f), new(-16.08f, -6.13f), new(-16.09f, -8.74f), new(-14.82f, -8.79f), new(-14.82f, -10.38f), new(-11.27f, -10.42f), new(-11.25f, -13.20f), new(-10.32f, -13.21f), new(-10.31f, -6.88f), new(-5.14f, -6.88f), new(-5.13f, -8.21f), new(-6.34f, -9.44f), new(-6.36f, -11.44f), new(-7.41f, -12.49f), new(-8.77f, -12.50f), new(-8.73f, -13.24f), new(-5.17f, -13.22f), new(-5.16f, -9.82f), new(-3.62f, -8.32f), new(-1.52f, -8.29f), new(-1.56f, -4.53f)]
        ];
}

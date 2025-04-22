using Virial.Game;

namespace Nebula.Map;

public class SkeldData : MapData
{
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
        new(-10.3f, -2.8f, MapObjectType.SmallOrTabletopOutOfSight), //メッドベイ左ベッド
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
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => NonMapPositions;
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
}

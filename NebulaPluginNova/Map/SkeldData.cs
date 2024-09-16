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
    static private MapObjectPoint[] mapObjectPoints = [
        new(0.1f, 5.8f, MapObjectType.SmallInCorner), //カフェ上
        new(-5.7f, 3.7f, MapObjectType.SmallInCorner), //カフェ左上
        new(-7.2f, -2.1f, MapObjectType.SmallInCorner), //メッドベイ右上
        new(-11.4f, 1.5f, MapObjectType.SmallInCorner), //メッドベイ上通路
        new(-18.4f, 2.4f, MapObjectType.SmallInCorner), //アッパーエンジン上
        new(-16.4f, -4.1f, MapObjectType.SmallInCorner), //十字路上方
        new(-17.3f, -6.9f, MapObjectType.SmallInCorner), //十字路下方
        new(-22.5f, -6.4f, MapObjectType.SmallInCorner), //リアクター中央下
        new(-12.2f, -4.2f, MapObjectType.SmallInCorner), //セキュ中央
        new(-15.2f, -9.5f, MapObjectType.SmallInCorner), //ロアエンジン上
        new(-17.1f, -13.5f, MapObjectType.SmallInCorner), //ロアエンジン下
        new(-12.5f, -14.7f, MapObjectType.SmallInCorner), //ロア電気室間
        new(-7.3f, -11.8f, MapObjectType.SmallInCorner), //電気室下
        new(-3.7f, -11.7f, MapObjectType.SmallInCorner), //ストレージ左上
        new(-0.8f, -14.1f, MapObjectType.SmallInCorner), //ストレージ右下
        new(5.3f, -9.8f, MapObjectType.SmallInCorner), //アドミン右下
        new(2.8f, -2.4f, MapObjectType.SmallInCorner), //カフェ右下
        new(9.9f, 0.3f, MapObjectType.SmallInCorner), //ウェポン
        new(12.2f, -2.9f, MapObjectType.SmallInCorner), //O2ナビ間
        new(18.1f, -5.7f, MapObjectType.SmallInCorner), //ナビ
        new(9.8f, -7.5f, MapObjectType.SmallInCorner), //ナビシールド間
        new(10.1f, -13.0f, MapObjectType.SmallInCorner), //シールド
        new(3.6f, -11.7f, MapObjectType.SmallInCorner), //コミュ前通路
        new(1.9f, -14.8f, MapObjectType.SmallInCorner), //コミュ
        ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => NonMapPositions;
    protected override SystemTypes[] SabotageTypes => new SystemTypes[] { SystemTypes.Reactor, SystemTypes.Comms, SystemTypes.Electrical, SystemTypes.LifeSupp };
}

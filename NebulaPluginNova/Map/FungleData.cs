using Virial.Game;

namespace Nebula.Map;


public class FungleData : MapData
{
    static private Vector2[] MapPositions = new Vector2[]
        { 
        //ドロップシップ
        new(-9.2f,13.4f),
        //カフェテリア
        new(-19.1f, 7.0f),new(-13.6f,5.0f),new(-20.5f,6.0f),
        //カフェ下
        new(-12.9f,2.3f),new(-21.7f,2.41f),
        //スプラッシュゾーン
        new(-20.2f,-0.3f),new(-19.8f,-2.1f),new(-16.1f,-0.1f),new(-15.6f,-1.8f),
        //キャンプファイア周辺
        new(-11.3f,2.0f),new(-0.83f,2.4f),new(-9.4f,0.2f),new(-6.9f,0.2f),
        //スプラッシュゾーン下
        new(-17.3f,-4.5f),
        //キッチン
        new(-15.4f,-9.5f),new(-17.4f,-7.5f),
        //キッチン・ジャングル間通路
        new(-11.2f,-6.1f),new(-5.5f,-14.8f),
        //ミーティング上
        new(-2.8f,2.2f),new(2.2f,1.0f),
        //ストレージ
        new(-0.6f,4.2f),new(2.3f,6.2f),new(3.3f,6.7f),
        //ミーティング・ドーム
        new(-0.15f,-1.77f),new(-4.65f,1.58f),new(-4.8f,-1.44f),
        //ラボ
        new(-7.1f,-11.9f),new(-4.5f,-6.8f),new(-3.3f,-8.9f),new(-5.4f,-10.2f),
        //ジャングル(左)
        new(-1.44f,-13.3f),new(3.8f,-12.5f),
        //ジャングル(中)
        new(7.08f,-15.3f),new(11.6f,-14.3f),
        //ジャングル(上)
        new(2.7f,-6.0f),new(12.1f,-7.3f),
        //グリーンハウス・ジャングル
        new(13.6f,-12.1f),new(6.4f,-10f),
        //ジャングル(右)
        new(15.0f,-6.7f),new(18.1f,-9.1f),
        //ジャングル(下)
        new(14.9f,-16.3f),
        //リアクター
        new(21.1f,-6.7f),
        //高台
        new(15.9f,0.4f),new(15.6f,4.3f),new(19.2f,1.78f),
        //鉱山
        new(12.5f,7.7f),new(13.4f,9.7f),
        //ルックアウト
        new(6.6f,3.8f),new(8.7f,1f),
        //梯子中間
        new(20.1f,7.2f),
        //コミュ
        new(20.9f,10.8f),new(24.1f,13.2f),new(17.9f,12.7f),
        };

    static private MapObjectPoint[] mapObjectPoints = [
        new(-6.9f, 12.8f, MapObjectType.SmallInCorner), //ドロップシップ上方
        new(-9.2f, 7.0f, MapObjectType.SmallInCorner), //ドロップシップ左下
        new(-13.6f, 4.8f, MapObjectType.SmallInCorner), //カフェテリア右下
        new(-17.9f, 7.5f, MapObjectType.SmallInCorner), //カフェテリア左上
        new(-20.5f, 6.5f, MapObjectType.SmallInCorner), //カフェテリア左外
        new(-21.5f, 2.0f, MapObjectType.SmallInCorner), //レク左上外
        new(-15.7f, 2.2f, MapObjectType.SmallInCorner), //レクカフェ間
        new(-16.2f, 0.9f, MapObjectType.SmallInCorner), //レクシャワー
        new(-18.5f, -2.3f, MapObjectType.SmallInCorner), //レク左下
        new(-23.0f, -1.5f, MapObjectType.SmallInCorner), //レク左外
        new(-14.6f, -2.4f, MapObjectType.SmallInCorner), //レク右下
        new(-19.3f, -4.6f, MapObjectType.SmallInCorner), //レクキッチン間
        new(-17.2f, -9.6f, MapObjectType.SmallInCorner), //キッチン下
        new(-21.5f, -6.7f, MapObjectType.SmallInCorner), //桟橋
        new(-9.7f, -9.6f, MapObjectType.SmallInCorner), //キッチン右ジャングル通路入り口
        new(-7.7f, -15.3f, MapObjectType.SmallInCorner), //ジャングル左下端
        new(-2.3f, -12.8f, MapObjectType.SmallInCorner), //ラボ下
        new(-7.6f, -7.5f, MapObjectType.SmallInCorner), //ラボ左
        new(-4.6f, -10.9f, MapObjectType.SmallInCorner), //ラボ中
        new(-1.6f, -6.8f, MapObjectType.SmallInCorner), //ラボ右上
        new(1.8f, -5.2f, MapObjectType.SmallInCorner), //会議室右下外
        new(-1.1f, -0.4f, MapObjectType.SmallInCorner), //会議室
        new(-7.3f, 4.0f, MapObjectType.SmallInCorner), //キャンプファイア右上
        new(1.6f, -2.4f, MapObjectType.SmallInCorner), //宿舎
        new(1.2f, 1.9f, MapObjectType.SmallInCorner), //会議室ストレージ間
        new(0.0f, 4.1f, MapObjectType.SmallInCorner), //ストレージ
        new(-2.1f, 7.1f, MapObjectType.SmallInCorner), //ドロップシップストレージ間
        new(17.8f, 11.4f, MapObjectType.SmallInCorner), //コミュ左下
        new(24.8f, 14.6f, MapObjectType.SmallInCorner), //コミュ右上
        new(18.2f, 7.2f, MapObjectType.SmallInCorner), //コミュ高台中間
        new(21.8f, 1.7f, MapObjectType.SmallInCorner), //エンジン下
        new(17.4f, 3.1f, MapObjectType.SmallInCorner), //巨大宝石右
        new(13.5f, 2.8f, MapObjectType.SmallInCorner), //巨大宝石左
        new(13.7f, 9.3f, MapObjectType.SmallInCorner), //採掘場
        new(10.1f, 4.5f, MapObjectType.SmallInCorner), //展望右
        new(7.2f, 2.4f, MapObjectType.SmallInCorner), //展望中
        new(15.0f, -0.6f, MapObjectType.SmallInCorner), //巨大宝石下方
        new(19.9f, 0.7f, MapObjectType.SmallInCorner), //エンジン外左下
        new(20.5f, -8.5f, MapObjectType.SmallInCorner), //リアクター下
        new(21.9f, -5.9f, MapObjectType.SmallInCorner), //リアクター上
        new(14.6f, -6.0f, MapObjectType.SmallInCorner), //リアクター外左側上
        new(16.8f, -9.6f, MapObjectType.SmallInCorner), //リアクター外左側下
        new(17.6f, -13.2f, MapObjectType.SmallInCorner), //リアクター温室中間
        new(14.7f, -17.0f, MapObjectType.SmallInCorner), //ジャングル下方
        new(10.6f, -8.5f, MapObjectType.SmallInCorner), //温室上
        new(13.4f, -12.6f, MapObjectType.SmallInCorner), //温室右
        new(7.7f, -14.7f, MapObjectType.SmallInCorner), //温室下
        new(2.3f, -11.4f, MapObjectType.SmallInCorner), //ラボ温室間
        ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => [];
    protected override SystemTypes[] SabotageTypes => new SystemTypes[] { SystemTypes.Reactor, SystemTypes.Comms };
}

using Virial.Game;

namespace Nebula.Map;


public class FungleData : MapData
{
    static private readonly Vector2[] MapPositions = [
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
        ];

    //先頭の部屋ほど優先される
    static private readonly (AdditionalRoomArea area, string key, bool detailRoom)[] additionalRooms = [
        (new(-5.42f, -9.20f, 3.32f, 3.72f), "jungleLabo", true),
        (new(16.75f, -6.68f, 3.03f, 1.23f), "jungleReactor", true),
        (new(17.79f, -10.59f, 1.99f, 3.09f), "jungleReactor", true),
        (new(9.07f, -11.00f, 5.07f, 3.89f), "jungleGreenhouse", true),
        (new(8.70f, -10.75f, 17.47f, 6.90f), "jungle", false),
        (new(-10.25f, -10.93f, 2.84f, 5.19f), "beachToJungle", false),
        (new(-9.72f, 1.39f, 2.87f, 2.07f), "campfire", false),
        (new(-16.33f, 5.10f, 6.35f, 4.05f), "beachCafeteria", true),
        (new(-22.00f, -0.84f, 3.10f, 2.55f), "beachSplashzone", true),
        (new(-16.33f, 5.10f, 6.35f, 4.05f), "beachCafeteria", true),
        (new(-10.51f, 1.25f, 14.82f, 9.13f), "beach", false),
        (new(21.60f, 12.72f, 5.78f, 2.97f), "highlandsComm", true),
        (new(15.96f, 6.89f, 11.03f, 8.87f), "highlands", false),
        ];
    static private readonly (SystemTypes room, AdditionalRoomArea area, string key)[] overrideRooms = [];

    static private readonly MapObjectPoint[] mapObjectPoints = [
        new(-11.2f, 12.4f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ上端左
        new(-9.6f, 13.6f, MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ上端
        new(-6.9f, 12.8f, MapObjectType.SmallInCorner), //ドロップシップ上方
        new(-9.2f, 7.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ左下
        new(-13.6f, 4.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //カフェテリア右下
        new(-17.9f, 7.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //カフェテリア左上
        new(-16.4f, 6.2f, MapObjectType.SmallOrTabletopOutOfSight), //カフェテリア卓上
        new(-20.5f, 6.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //カフェテリア左外
        new(-21.5f, 2.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //レク左上外
        new(-15.7f, 2.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //レクカフェ間
        new(-18.1f, 2.4f, MapObjectType.SmallOrTabletopOutOfSight), //レクカフェ間左寄り
        new(-16.2f, 0.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //レクシャワー
        new(-18.5f, -2.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //レク左下
        new(-23.0f, -1.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //レク左外
        new(-14.6f, -2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //レク右下
        new(-15.8f, -2.3f, MapObjectType.SmallOrTabletopOutOfSight), //レク下卓上
        new(-17.3f, -0.1f, MapObjectType.Reachable), //レク中央
        new(-19.3f, -4.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //レクキッチン間
        new(-13.9f, -7.6f, MapObjectType.Reachable), //キッチン中央
        new(-17.2f, -9.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //キッチン下
        new(-21.5f, -6.7f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //桟橋
        new(-20.9f, -7.2f, MapObjectType.Reachable), //桟橋(中央寄り)
        new(-9.7f, -9.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //キッチン右ジャングル通路入り口
        new(-7.7f, -15.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ジャングル左下端
        new(-2.3f, -12.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボ下
        new(-7.6f, -7.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ラボ左
        new(-4.6f, -10.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボ中
        new(-1.6f, -6.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボ右上
        new(1.8f, -5.2f, MapObjectType.SmallInCorner), //会議室右下外
        new(0.2f, -6.8f, MapObjectType.SmallOrTabletopOutOfSight), //会議室右下外下寄り
        new(-1.1f, -0.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //会議室
        new(-7.3f, 4.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //キャンプファイア右上
        new(1.6f, -2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //宿舎
        new(1.2f, -0.7f, MapObjectType.SmallOrTabletopOutOfSight), //宿舎上寄り
        new(1.2f, 1.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //会議室ストレージ間
        new(0.0f, 4.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ストレージ
        new(-2.2f, 7.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップストレージ間
        new(17.8f, 11.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コミュ左下
        new(24.8f, 14.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コミュ右上
        new(22.7f, 13.2f, MapObjectType.Reachable), //コミュ中央
        new(18.2f, 7.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コミュ高台中間
        new(21.8f, 1.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //エンジン下
        new(17.4f, 3.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //巨大宝石右
        new(13.5f, 2.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //巨大宝石左
        new(13.7f, 9.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //採掘場
        new(11.2f, 8.2f, MapObjectType.SmallOrTabletopOutOfSight), //採掘場左寄り
        new(10.1f, 4.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //展望右
        new(7.2f, 2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //展望中
        new(10.0f, 1.2f, MapObjectType.SmallOrTabletopOutOfSight),//展望奥
        new(15.0f, -0.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //巨大宝石下方
        new(19.9f, 0.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //エンジン外左下
        new(21.8f, 1.7f, MapObjectType.SmallOrTabletopOutOfSight), //エンジン下
        new(20.5f, -8.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //リアクター下
        new(21.9f, -5.9f, MapObjectType.SmallInCorner), //リアクター上
        new(14.6f, -6.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //リアクター外左側上
        new(16.8f, -9.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //リアクター外左側下
        new(18.9f, -12.6f, MapObjectType.SmallOrTabletopOutOfSight), //リアクター外左側下隅
        new(17.6f, -13.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //リアクター温室中間
        new(14.7f, -17.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ジャングル下方
        new(10.6f, -8.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //温室上
        new(13.4f, -12.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //温室右
        new(7.7f, -14.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //温室下
        new(5.8f, -7.4f, MapObjectType.Reachable), //温室左上
        new(3.7f, -13.1f, MapObjectType.Reachable), //温室左下
        new(2.3f, -11.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボ温室間
        ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => [];
    protected override (AdditionalRoomArea area, string key, bool detailRoom)[] AdditionalRooms => additionalRooms;
    protected override (SystemTypes room, AdditionalRoomArea area, string key)[] OverrideRooms => overrideRooms;
    protected override SystemTypes[] SabotageTypes => [SystemTypes.Reactor, SystemTypes.Comms];
    override public Vector2[][] RaiderIgnoreArea
    {
        get => [
            [new(26.9f, 10.9f),new(26.9f, 4.2f), new(15.9f,4.2f),new(15.9f, 10.9f)],//高台コミュ間
            [new(19.0f, 0.0f),new(19.0f, -5.9f), new(15.1f,-5.9f),new(15.1f, 0.0f)],//高台下左
            [new(11.9f, 0.0f),new(11.9f, -2.0f), new(11.1f, -2.0f),new(11.1f, 0.0f)],//高台下右
            [new(11.9f, -5.0f),new(11.9f, -6.1f), new(10.5f, -6.1f),new(10.5f, -5.0f)],//高台下右ジャングル側
            [new(4.9f, 2.1f),new(4.9f, 0.3f), new(3.2f,0.3f),new(3.2f, 2.1f)],//セキュ左下層側
            [new(6.9f, 1.5f),new(6.9f, -1.2f), new(4.5f, -1.2f),new(4.5f, 1.5f)],//セキュ左上層側
        ];
    }


    private static readonly IDividedSpriteLoader SealedVentSpriteFungle = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedVentFungle.png", 100f, 8, 2);
    protected override IDividedSpriteLoader SealedVentSprite => SealedVentSpriteFungle;

    internal static readonly IDividedSpriteLoader SealedDoorSpriteFungleH = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorFungleH.png", 100f, 8, 2);
    internal static readonly IDividedSpriteLoader SealedDoorSpriteFungleV = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorFungleV.png", 100f, 8, 2);
    protected override IDividedSpriteLoader GetSealedDoorSprite(bool isVert) => isVert ? SealedDoorSpriteFungleV : SealedDoorSpriteFungleH;
    override public Vector3 GetDoorSealingPos(OpenableDoor door, bool isVert) => isVert ? new(-0.07f, -0.4f, -0.01f) : new(-0.02f, -0.4f, -0.01f);

    private readonly Virial.Utilities.ComponentCache<AmbientSoundPlayer> beachFar = new(() => ShipStatus.Instance.transform.TryDig("Outside", "OutsideBeach", "SFX", "AMB_Beach_Far")?.GetComponent<AmbientSoundPlayer>()!);
    private readonly Virial.Utilities.ComponentCache<AmbientSoundPlayer> beachClose = new(() => ShipStatus.Instance.transform.TryDig("Outside", "OutsideBeach", "SFX", "AMB_Beach_Close")?.GetComponent<AmbientSoundPlayer>()!);
    private readonly Virial.Utilities.ComponentCache<AmbientSoundPlayer> highlands = new(() => ShipStatus.Instance.transform.TryDig("Outside", "OutsideHighlands", "SFX", "AMB_Outside")?.GetComponent<AmbientSoundPlayer>()!);
    public override WindType GetWindType(Vector2 position)
    {
        if (
            (beachFar.Get()?.HitAreas.Any(area => area.OverlapPoint(position)) ?? false) ||
            (beachClose.Get()?.HitAreas.Any(area => area.OverlapPoint(position)) ?? false)
            )
        {
            return WindType.FungleBeach;
        }
        if (
            (highlands.Get()?.HitAreas.Any(area => area.OverlapPoint(position)) ?? false)
            )
        {
            return WindType.FungleHighlands;
        }
        return WindType.NoWind;
    }
}

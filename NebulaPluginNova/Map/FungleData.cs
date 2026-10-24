using Hazel;
using Rewired.Utils.Classes.Data;
using Virial.Game;

namespace Nebula.Map;


public class FungleData : MapData
{
    override public int Id => 5;
    static private readonly (VVector2 pos, int inMapFlag)[] MapPositions = [
        //ドロップシップ
        (new(-9.2f,13.4f), 0x3F),
        //カフェテリア
        (new(-19.1f, 7.0f), 0x0F), (new(-13.6f,5.0f), 0x0F), (new(-20.5f,6.0f), 0x0F),
        //カフェ下
        (new(-12.9f,2.3f), 0x0F), (new(-21.7f,2.41f), 0x0F),
        //スプラッシュゾーン
        (new(-20.2f,-0.3f), 0x0F), (new(-19.8f,-2.1f), 0x0F), (new(-16.1f,-0.1f), 0x0F), (new(-15.6f,-1.8f), 0x0F),
        //キャンプファイア周辺
        (new(-11.3f,2.0f), 0x3F), (new(-0.83f,2.4f), 0x3F), (new(-9.4f,0.2f), 0x3F), (new(-6.9f,0.2f), 0x3F),
        //スプラッシュゾーン下
        (new(-17.3f,-4.5f), 0x0F),
        //キッチン
        (new(-15.4f,-9.5f), 0x0F), (new(-17.4f,-7.5f), 0x0F),
        //キッチン・ジャングル間通路
        (new(-11.2f,-6.1f), 0x3F), (new(-5.5f,-14.8f), 0x3F),
        //ミーティング上
        (new(-2.8f,2.2f), 0x3F), (new(2.2f,1.0f), 0x3F),
        //ストレージ
        (new(-0.6f,4.2f), 0x3F), (new(2.3f,6.2f), 0x3F), (new(3.3f,6.7f), 0x3F),
        //ミーティング・ドーム
        (new(-0.15f,-1.77f), 0xFF), (new(-4.65f,1.58f), 0xFF), (new(-4.8f,-1.44f), 0xFF),
        //ラボ
        (new(-2.7f, -12.1f), 0xFF), (new(-7.3f, -9.1f), 0xFF), (new(-4.5f,-6.8f), 0xFF), (new(-3.3f,-8.9f), 0xFF), (new(-5.4f,-10.2f), 0xFF),
        //ジャングル(左)
        (new(-1.44f,-13.3f), 0xFF),
        //ジャングル(中)
        (new(11.6f,-14.3f), 0xCF), (new(10.78f, -15.96f), 0xCF),
        //ジャングル(上)
        (new(2.7f,-6.0f), 0xFF),(new(12.1f,-7.3f), 0xCF),
        //グリーンハウス・ジャングル
        (new(13.6f,-12.1f), 0xCF), (new(6.4f,-9.9f), 0xCF),
        //ジャングル(右)
        (new(15.0f,-6.7f), 0xCF), (new(18.1f,-9.1f), 0xCF),
        //ジャングル(下)
        (new(14.9f,-16.3f), 0xCF),
        //リアクター
        (new(21.1f,-6.7f), 0xCF),
        //高台
        (new(15.9f,0.4f), 0xCF), (new(15.6f,4.3f), 0xCF), (new(19.2f,1.78f), 0xCF),
        //鉱山
        (new(12.5f,7.7f), 0xCF), (new(13.4f,9.7f), 0xCF),
        //ルックアウト
        (new(6.6f,3.8f), 0xCF), (new(8.7f,1f), 0xCF),
        //梯子中間
        (new(20.1f,7.2f), 0xCF),
        //コミュ
        (new(20.9f,10.8f), 0xCF), (new(24.1f,13.2f), 0xCF), (new(17.9f,12.7f), 0xCF),
        //NonClassic
        (new(3.8f,-12.5f), 0xC3),(new(7.08f,-15.3f), 0xC3),
        //Classic
        (new(0.35f, -10.8f), 0xCC), (new(12.65f, 4.00f), 0xCC),
        ];

    static private VVector2[] GetMapPos(int inMapId) => MapPositions.Where(entry => (entry.inMapFlag & (1 << inMapId)) != 0).Select(entry => entry.pos).ToArray();
    static private readonly VVector2[] combinedNonClassicMapPos = GetMapPos(0);
    static private readonly VVector2[] combinedClassicMapPos = GetMapPos(2);
    static private readonly VVector2[] combinedMidnightMapPos = GetMapPos(4);
    static private readonly VVector2[] combinedPoisonedMapPos = GetMapPos(6);

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

    static private readonly (MapObjectPoint mop, int inMapIdFlag)[] mapObjectPoints = [
        (new(-11.2f, 12.4f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //ドロップシップ上端左
        (new(-9.6f, 13.6f, MapObjectType.SmallOrTabletopOutOfSight | MapObjectType.SmallInCorner), 0x3F), //ドロップシップ上端
        (new(-6.9f, 12.8f, MapObjectType.SmallInCorner), 0x3F), //ドロップシップ上方
        (new(-9.2f, 7.0f, MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //ドロップシップ左下
        (new(-8.2f, 8.3f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x3F), //ドロップシップ下方
        (new(-13.6f, 4.8f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x0F), //カフェテリア右下
        (new(-17.9f, 7.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //カフェテリア左上
        (new(-16.4f, 6.2f, MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //カフェテリア卓上
        (new(-20.5f, 6.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //カフェテリア左外
        (new(-21.5f, 2.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //レク左上外
        (new(-15.7f, 2.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0x0F), //レクカフェ間
        (new(-18.1f, 2.4f, MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //レクカフェ間左寄り
        (new(-16.2f, 0.9f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x0F), //レクシャワー
        (new(-18.5f, -2.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0x0F), //レク左下
        (new(-23.0f, -1.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //レク左外
        (new(-14.6f, -2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0x0F), //レク右下
        (new(-15.8f, -2.3f, MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //レク下卓上
        (new(-14.3f, 0.6f, MapObjectType.DepoisonBox),0x0F),//レク上卓上
        (new(-17.3f, -0.1f, MapObjectType.Reachable), 0x0F), //レク中央
        (new(-19.3f, -4.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //レクキッチン間
        (new(-16.3f, -4.0f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x0F), //レク下外側
        (new(-13.9f, -7.6f, MapObjectType.Reachable), 0x0F), //キッチン中央
        (new(-17.2f, -9.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //キッチン下
        (new(-18.15f, -6.7f, MapObjectType.SmallOpened | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //キッチン左上
        (new(-21.9f, -6.9f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), 0x0F), //桟橋
        (new(-20.9f, -7.2f, MapObjectType.Reachable), 0x0F), //桟橋(中央寄り)
        (new(-9.7f, -9.6f, MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //キッチン右ジャングル通路入り口下
        (new(-8.5f, -13.45f, MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //キッチン右ジャングル通路入り口上
        (new(-7.7f, -15.3f, MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //ジャングル左下端
        (new(-2.3f, -12.8f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xFF), //ラボ下
        (new(-7.6f, -7.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xFF), //ラボ左
        (new(-7.02f, -10.9f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), 0xFF), //ラボ左下寄り
        (new(-4.6f, -10.9f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xFF), //ラボ中
        (new(-1.6f, -6.8f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xFF), //ラボ右上
        (new(-5.4f, -10.2f, MapObjectType.DepoisonBox), 0xFF), //ラボ左下の床
        (new(-0.3f, -9.4f, MapObjectType.SmallOpened), 0xCF), //ラボ右の通路
        (new(1.8f, -5.2f, MapObjectType.SmallOpened), 0xFF), //会議室右下外
        (new(0.2f, -6.8f, MapObjectType.SmallOrTabletopOutOfSight), 0xFF), //会議室右下外下寄り
        (new(-1.1f, -0.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xFF), //会議室
        (new(-7.3f, 4.0f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x3F), //キャンプファイア右上
        (new(-11.2f, -3.6f, MapObjectType.SmallOpened), 0x3F), //キャンプファイア下の壁
        (new(1.6f, -2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xFF), //宿舎
        (new(1.2f, -0.7f, MapObjectType.SmallOrTabletopOutOfSight), 0xFF), //宿舎上寄り
        (new(-6.8f, -1.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0x3F), //会議室外左
        (new(1.2f, 1.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //会議室ストレージ間
        (new(1.16f, 0.82f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //会議室ストレージ間壁下
        (new(-2.1f, 2.3f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x3F), //ストレージ前
        (new(0.0f, 4.1f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0x3F), //ストレージ
        (new(1.5f, 3.9f, MapObjectType.SmallInCorner), 0x3F), //ストレージ奥
        (new(-2.2f, 7.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0x3F), //ドロップシップストレージ間
        (new(17.8f, 11.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //コミュ左下
        (new(24.8f, 14.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //コミュ右上
        (new(22.7f, 13.2f, MapObjectType.Reachable), 0xCF), //コミュ中央
        (new(22.3f, 11.5f, MapObjectType.SmallOpened), 0xCF), //コミュ下
        (new(18.2f, 7.2f, MapObjectType.SmallInCorner | MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //コミュ高台中間左
        (new(22.4f, 8.14f, MapObjectType.SmallInCorner | MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //コミュ高台中間右
        (new(21.8f, 1.7f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xCF), //エンジン下
        (new(17.4f, 3.1f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xCF), //巨大宝石右
        (new(13.5f, 2.8f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xCF), //巨大宝石左
        (new(13.7f, 9.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //採掘場
        (new(11.2f, 8.2f, MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //採掘場左寄り
        (new(10.1f, 4.5f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xCF), //展望右
        (new(7.2f, 2.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //展望中
        (new(10.0f, 1.2f, MapObjectType.SmallOrTabletopOutOfSight), 0xCF),//展望奥
        (new(15.0f, -0.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //巨大宝石下方
        (new(15.5f, 1.12f, MapObjectType.SmallOpened), 0xCF), //巨大宝石下そば
        (new(19.9f, 0.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //エンジン外左下
        (new(21.8f, 1.7f, MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //エンジン下
        (new(20.5f, -8.5f, MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //リアクター下
        (new(21.9f, -5.9f, MapObjectType.SmallInCorner), 0xCF), //リアクター上
        (new(14.6f, -6.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //リアクター外左側上
        (new(16.8f, -9.6f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xCF), //リアクター外左側下
        (new(18.9f, -12.6f, MapObjectType.SmallOrTabletopOutOfSight), 0xCF), //リアクター外左側下隅
        (new(17.6f, -13.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //リアクター温室中間
        (new(14.7f, -17.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //ジャングル下方
        (new(18.8f, -8.6f, MapObjectType.SmallOpened), 0xCF), //リアクター前
        (new(6.9f, -6.2f, MapObjectType.SmallOpened), 0xFF), //ジャングル上方
        (new(10.6f, -8.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), 0xCF), //温室上
        (new(7.9f, -7.7f, MapObjectType.Reachable), 0xCF), //温室上の通路中央
        (new(13.4f, -12.6f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xCF), //温室右
        (new(5.8f, -7.4f, MapObjectType.SmallInCorner), 0xCF), //温室左上

        // NonClassic
        (new(7.7f, -14.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xC3), //温室下
        (new(3.7f, -13.1f, MapObjectType.Reachable), 0xC3), //温室左下
        (new(2.3f, -11.4f, MapObjectType.SmallOpened | MapObjectType.Reachable), 0xC3), //ラボ温室間

        // Classic
        (new(0.59f, -11.9f, MapObjectType.SmallOpened | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), 0xCC), //ラボ外右下
        (new(10.0f, -16.0f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), 0xCC), //O2下
        (new(9.5f, -13.8f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), 0xCC), //O2下、上寄り
        ];


    public override MapObjectPoint[] MapObjectPoints { get {
            var flag = 1 << NebulaAsset.GetInMapId(5);
            return mapObjectPoints.Where(mop => (mop.inMapIdFlag & flag) != 0).Select(mop => mop.mop).ToArray();
        } 
    }

    public override IReadOnlyList<VVector2> MapArea => NebulaAsset.GetInMapId(5) switch
    {
        0 or 1 => combinedNonClassicMapPos,
        2 or 3 => combinedClassicMapPos,
        4 or 5 => combinedMidnightMapPos,
        6 or 7 => combinedPoisonedMapPos,
        _ => combinedNonClassicMapPos
    };

    public override IReadOnlyList<VVector2> NonMapArea => [];
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
            [new(-19.5f, -7.35f),new(-24.5f, -7.1f),new(-24.5f, 1.48f),new(-20.5f, -4.0f)],//桟橋上側
        ];
    }


    private static SystemTypes[] UnusedRoomsMidnight => [SystemTypes.Comms, SystemTypes.Lookout, SystemTypes.MiningPit, SystemTypes.UpperEngine, SystemTypes.Greenhouse, SystemTypes.Cafeteria, SystemTypes.RecRoom, SystemTypes.Kitchen, SystemTypes.Highlands];
    private static SystemTypes[] UnusedRoomsPoisoned => [SystemTypes.Storage, SystemTypes.Dropship, SystemTypes.Cafeteria, SystemTypes.RecRoom, SystemTypes.Kitchen, SystemTypes.FishingDock, SystemTypes.Beach];
    public override SystemTypes[] UnusedRooms => GeneralConfigurations.FungleVariation switch
    {
        1 => UnusedRoomsMidnight,
        2 => UnusedRoomsPoisoned,
        _ => []
    };

    private static SystemTypes[] UnrecommendedRoomsMidnight => [SystemTypes.Reactor, SystemTypes.FishingDock, SystemTypes.Jungle];
    private static SystemTypes[] UnrecommendedRoomsPoisoned => [];
    public override SystemTypes[] UnrecommendedRooms => GeneralConfigurations.FungleVariation switch
    {
        1 => UnrecommendedRoomsMidnight,
        2 => UnrecommendedRoomsPoisoned,
        _ => []
    };

    private static readonly IDividedSpriteLoader SealedVentSpriteFungle = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedVentFungle.png", 100f, 8, 2);
    protected override IDividedSpriteLoader SealedVentSprite => SealedVentSpriteFungle;

    internal static readonly IDividedSpriteLoader SealedDoorSpriteFungleH = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorFungleH.png", 100f, 8, 2);
    internal static readonly IDividedSpriteLoader SealedDoorSpriteFungleV = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorFungleV.png", 100f, 8, 2);
    protected override IDividedSpriteLoader GetSealedDoorSprite(bool isVert) => isVert ? SealedDoorSpriteFungleV : SealedDoorSpriteFungleH;
    override public Vector3 GetDoorSealingPos(OpenableDoor door, bool isVert) => isVert ? new(-0.07f, -0.4f, -0.01f) : new(-0.02f, -0.4f, -0.01f);

    private readonly Virial.Utilities.ComponentCache<AmbientSoundPlayer> beachFar = new(() => AmongUsLLImpl.ShipStatusInstance.transform.TryFindChild(out var found, "Outside", "OutsideBeach", "SFX", "AMB_Beach_Far") ? found.GetComponent<AmbientSoundPlayer>() : null!);
    private readonly Virial.Utilities.ComponentCache<AmbientSoundPlayer> beachClose = new(() => AmongUsLLImpl.ShipStatusInstance.transform.TryFindChild(out var found, "Outside", "OutsideBeach", "SFX", "AMB_Beach_Close") ? found.GetComponent<AmbientSoundPlayer>() : null!);
    private readonly Virial.Utilities.ComponentCache<AmbientSoundPlayer> highlands = new(() => AmongUsLLImpl.ShipStatusInstance.transform.TryFindChild(out var found, "Outside", "OutsideHighlands", "SFX", "AMB_Outside") ? found.GetComponent<AmbientSoundPlayer>() : null!);
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

    NavVerticesStructure? normalNav = null, modifiedNav = null;
    public override NavVerticesStructure MapNavData
    {
        get
        {
            if (GeneralConfigurations.FungleForClassicGameOption.Value)
            {
                if (modifiedNav == null)
                {
                    using var stream = StreamHelper.OpenFromResource("Nebula.Resources.Pathfinding.fungleModified.json");
                    if (stream != null) modifiedNav = JsonStructure.Deserialize<NavVerticesStructure>(stream);
                }
                return modifiedNav!;
            }
            else
            {
                if (normalNav == null)
                {
                    using var stream = StreamHelper.OpenFromResource("Nebula.Resources.Pathfinding.fungle.json");
                    if (stream != null) normalNav = JsonStructure.Deserialize<NavVerticesStructure>(stream);
                }
                return normalNav!;
            }
        }
    }

    public override (SystemTypes room, Vector2 pos, int inMapIdMask)[] AdminRooms { get; } = [
        (SystemTypes.RecRoom, new(-17.7f, -0.6f), 0x0F),
        (SystemTypes.FishingDock, new(-21.2f,-7.3f), 0x3F),
        (SystemTypes.Kitchen, new(-15.2f, -8.1f), 0x0F),
        (SystemTypes.Laboratory, new(-4.2f, -9.1f), 0xFF),
        (SystemTypes.Greenhouse, new(9.3f, -11.2f), 0xCF),
        (SystemTypes.Reactor, new(22.2f,-7.2f), 0xCF),
        (SystemTypes.MeetingRoom, new(-2.8f,-1.2f), 0xFF),
        (SystemTypes.SleepingQuarters, new(2.5f, -1.4f), 0xFF),
        (SystemTypes.Storage, new(1.7f, 5.4f), 0x3F),
        (SystemTypes.Comms, new(21.7f, 12.9f), 0xCF),
        (SystemTypes.UpperEngine, new(22.7f, 3.0f), 0xCF),
        (SystemTypes.MiningPit, new(12.9f, 9.2f), 0xCF),
        (SystemTypes.Lookout, new(8.4f, 2.3f), 0xCF),
        (SystemTypes.Dropship, new(-7.8f, 11.1f), 0x3F),
        (SystemTypes.Cafeteria, new(-16.0f, 6.1f), 0x3F),
        (SystemTypes.Reactor, new(4.0f, -10.0f), 0x30),
        (SystemTypes.FishingDock, new(-13.2f, -9.0f), 0x30),
        ];
}

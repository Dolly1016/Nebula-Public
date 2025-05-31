using Virial.Game;

namespace Nebula.Map;

public class AirshipData : MapData
{
    static private readonly Vector2[] MapPositions = [ 
        //金庫
        new(-9f, 12.8f), new(-8.7f, 4.9f), new(-12.8f, 8.7f), new(-4.8f, 8.7f), new(-7.1f, 6.8f), new(-10.4f, 6.9f), new(-7f, 10.2f),
        //宿舎前
        new(-0.5f, 8.5f),
        //エンジン上
        new(-0.4f, 5f),
        //エンジン
        new(0f, -1.4f), new(3.6f, 0.1f), new(0.4f, -2.5f), new(-6.9f, 1.1f),
        //コミュ前
        new(-11f, -1f),
        //コミュ
        new(-12.3f, 0.9f),
        //コックピット
        new(-19.9f, -2.6f), new(-19.9f, 0.5f),
        //武器庫
        new(-14.5f, -3.6f), new(-9.9f, -6f), new(-15f, -9.4f),
        //キッチン
        new(-7.5f, -7.5f), new(-7f, -12.8f), new(-2.5f, -11.2f), new(-3.9f, -9.3f),
        //左展望
        new(-13.8f, -11.8f),
        //セキュ
        new(7.3f, -12.3f), new(5.8f, -10.6f),
        //右展望
        new(10.3f, -15f),
        //エレク
        new(10.5f, -8.5f),
        //エレクの9部屋
        new(10.5f, -6.3f), new(13.5f, -6.3f), new(16.5f, -6.3f), new(19.4f, -6.3f), new(13.5f, -8.8f), new(16.5f, -8.8f), new(19.4f, -8.8f), new(16.5f, -11f), new(19.4f, -11f),
        //エレク右上
        new(19.4f, -4.2f),
        //メディカル
        new(25.2f, -9.8f), new(22.9f, -6f), new(25.2f, -9.8f), new(29.5f, -6.3f),
        //貨物
        new(31.8f, -3.3f), new(34f, 1.4f), new(39f, -0.9f), new(37.6f, -3.4f), new(32.8f, 3.6f), new(35.3f, 3.6f),
        //ロミジュリ右
        new(29.8f, -1.5f),
        //ラウンジ
        new(33.7f, 7.1f), new(32.4f, 7.1f), new(30.9f, 7.1f), new(29.2f, 7.1f), new(30.8f, 5.3f), new(24.9f, 4.9f), new(27.1f, 7.3f),
        //レコード
        new(22.3f, 9.1f), new(20f, 11.5f), new(17.6f, 9.4f), new(20.1f, 6.6f),
        //ギャップ右
        new(15.4f, 9.2f), new(11.2f, 8.5f), new(12.6f, 6.2f),
        //シャワー/ロミジュリ左
        new(18.9f, 4.5f), new(17.2f, 5.2f), new(18.5f, 0f), new(21.2f, -2f), new(24f, 0.7f), new(22.3f, 2.5f),
        //メインホール
        new(10.8f, 0f), new(14.8f, 1.9f), new(11.8f, 1.8f), new(9.7f, 2.5f), new(6.2f, 2.4f), new(6.6f, -3f), new(12.7f, -2.9f),
        //ギャップ左
        new(3.8f, 8.8f),
        //ミーティング
        new(6.5f, 15.3f), new(11.8f, 14.1f), new(11.8f, 16f), new(16.3f, 15.2f),
        ];

    static private readonly (AdditionalRoomArea area, string key, bool detailRoom)[] additionalRooms = [
        (new(-12.50f, -1.01f, 4.36f, 1.61f), "commHall", false)
        ];
    static private readonly (SystemTypes room, AdditionalRoomArea area, string key)[] overrideRooms = [
        (SystemTypes.Security, new(8.04f, -15.64f, 3.45f, 2.11f), "securityDeck"),
        (SystemTypes.Engine, new(-1.08f, 4.78f, 1.99f, 3.34f), "engineUpper"),
        (SystemTypes.Showers, new(22.52f, 2.85f, 2.59f, 1.09f), "showerInner"),
        (SystemTypes.CargoBay, new(37.83f, -3.12f, 2.36f, 1.41f), "cargoSafe"),
        (SystemTypes.MeetingRoom, new(16.56f, 15.66f, 1.84f, 1.84f), "meetingRight")
        ];

    static private readonly MapObjectPoint[] mapObjectPoints = [
        new(-9.9f, 12.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //金庫上
        new(-5.8f, 5.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //金庫右下
        new(-8.7f, 4.9f, MapObjectType.SmallOrTabletopOutOfSight), //金庫下
        new(-6.3f, 12.1f, MapObjectType.SmallOrTabletopOutOfSight), //金庫右上
        new(1.5f, 8.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //宿舎右下
        new(5.5f, 9.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //昇降機左
        new(3.4f, 14.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ミーティング左
        new(14.0f, 14.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ミーティング
        new(17.2f, 15.0f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ミーティング右端
        new(-0.2f, 4.0f, MapObjectType.SmallInCorner), //エンジン上
        new(-6.3f, 0.8f, MapObjectType.SmallOrTabletopOutOfSight), //エンジン左上通路入り口
        new(-5.9f, -0.5f, MapObjectType.SmallInCorner), //エンジン左
        new(-1.6f,1.1f, MapObjectType.Reachable), //エンジン中央上
        new(-15.7f, -0.2f, MapObjectType.SmallInCorner), //コミュ左下外
        new(-12.1f, 0.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コミュ右下
        new(-17.1f, 1.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コックピット右上
        new(-20.7f, -3.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コックピット下
        new(-24.5f, -1.1f, MapObjectType.SmallOrTabletopOutOfSight), //コックピット左端
        new(-12.5f, -3.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //武器庫上
        new(-14.0f, -5.0f, MapObjectType.SmallOrTabletopOutOfSight), //武器庫上左側
        new(-12.6f, -9.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //武器庫下
        new(-14.5f, -7.9f, MapObjectType.SmallOrTabletopOutOfSight), //武器庫下左側
        new(-6.9f, -6.0f, MapObjectType.SmallInCorner), //キッチン上
        new(-6.1f, -12.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //キッチン下
        new(-11.6f, -11.2f, MapObjectType.SmallInCorner), //展望デッキ上
        new(-11.8f, -12.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //展望デッキ中央
        new(-13.0f, -14.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //展望デッキ下
        new(-16.7f, -12.9f, MapObjectType.SmallOrTabletopOutOfSight), //展望デッキ左
        new(-11.1f, -11.6f, MapObjectType.SmallOrTabletopOutOfSight), //展望配電盤右
        new(3.9f, -11.8f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //ポートレート右
        new(1.5f, -12.3f, MapObjectType.Reachable), //ポートレート中央
        new(5.6f, -14.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //セキュ下デッキ
        new(10.4f, -14.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //セキュ下デッキ右
        new(9.6f, -6.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //電気室左上
        new(19.3f, -4.2f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //電気室アスタリスク
        new(19.8f, -11.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //電気室右下
        new(13.3f, -10.8f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //電気室左下
        new(23.3f, -10.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //医務室下
        new(29.1f, -5.9f, MapObjectType.Reachable), //医務室上
        new(25.7f, -10.1f, MapObjectType.SmallOrTabletopOutOfSight), //医務室右下
        new(34.3f, -1.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //貨物中央
        new(37.9f, -3.7f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //貨物金庫
        new(39.6f, 0.0f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //貨物右
        new(29.0f, -2.1f, MapObjectType.SmallOrTabletopOutOfSight), //通気口右
        new(34.7f, 5.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ラウンジ右
        new(24.5f, 6.3f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラウンジ左
        new(22.6f, 10.5f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //アーカイブ右
        new(20.9f, 12f, MapObjectType.SmallOrTabletopOutOfSight), //アーカイブ右上
        new(20.7f, 8.4f, MapObjectType.Reachable), //アーカイブ右下
        new(17.6f, 10.5f, MapObjectType.SmallOrTabletopOutOfSight), //アーカイブ左上
        new(15.2f, 8.3f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //昇降機右端
        new(13.4f, 8.4f, MapObjectType.Reachable), //昇降機右
        new(13.6f, 5.8f, MapObjectType.SmallOrTabletopOutOfSight), //昇降機右下
        new(24.5f, 0.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //シャワー右
        new(17.9f, 4.9f, MapObjectType.SmallInCorner), //シャワー上
        new(21.0f, -2.2f, MapObjectType.SmallOrTabletopOutOfSight), //シャワー下
        new(20.6f, 2.5f, MapObjectType.SmallOrTabletopOutOfSight), //シャワー通路奥
        new(20.9f, 2.5f, MapObjectType.SmallOrTabletopOutOfSight), //シャワー修理タスク
        new(8.5f, 1.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //メイン中央上
        new(6.7f, 2.8f, MapObjectType.SmallOrTabletopOutOfSight), //メイン左上の部屋右側
        new(5.7f, 2.5f, MapObjectType.SmallInCorner), //メイン左上の部屋左側
        new(5.8f, -3.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //メイン左下
        new(13.0f, -2.1f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //メイン電気間
        new(1.7f, -1.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //エンジン右下
        ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => [];
    protected override (AdditionalRoomArea area, string key, bool detailRoom)[] AdditionalRooms => additionalRooms;
    protected override (SystemTypes room, AdditionalRoomArea area, string key)[] OverrideRooms => overrideRooms;

    protected override SystemTypes[] SabotageTypes => [SystemTypes.HeliSabotage, SystemTypes.Comms, SystemTypes.Electrical];
    override public Vector2[][] RaiderIgnoreArea { get => [
        [new(9.87f,9.78f),new(9.87f,7.8f), new(5.81f,7.8f),new(5.81f,9.78f)],//昇降機上
        [new(10.64f,6.39f), new(10.64f, 5.49f), new(10.1f, 5.49f), new(10.1f, 6.39f)],//昇降機下
        [new(26.66f,1.16f),new(28.17f,1.16f),new(28.17f,-2.5f),new(26.66f,-2.5f)],//通気口
        [new(3.92f,14.25f),new(5.13f,14.25f), new(5.13f, 13.85f),new(3.92f,13.85f)],//ミーティング
        [new(9.8f, -14.5f), new(9.8f, -17.5f), new(4.2f, -17.5f), new(4.2f, -14.5f)],//セキュ下展望
        [new(5.5f, -14.5f), new(5.5f, -17.5f), new(4.8f, -17.5f), new(4.8f, -14.5f)],//セキュ下展望左端
        [new(-11.7f, -14f), new(-11.7f, -18f), new(-13.7f, -18f), new(-13.7f, -14f)],//左展望

        ]; }

    protected override IDividedSpriteLoader SealedVentSprite => SkeldData.SealedVentSpriteSkeld;
    private HashSet<string> altSealedVents = ["EjectionVent", "HallwayVent2"];
    public override Sprite GetSealedVentSprite(Vent vent, int level, bool remove)
    {
        return base.GetSealedVentSprite(vent, altSealedVents.Contains(vent.name) ? (8 + level) : level, remove);
    }

    internal static IDividedSpriteLoader SealedDoorSpriteAirshipH = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorAirshipH.png", 100f, 8, 1);
    internal static IDividedSpriteLoader SealedDoorSpriteAirshipV = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedDoorAirshipV.png", 100f, 8, 2);
    protected override IDividedSpriteLoader GetSealedDoorSprite(bool isVert) => isVert ? SealedDoorSpriteAirshipV : SealedDoorSpriteAirshipH;
    override public Vector3 GetDoorSealingPos(OpenableDoor door, bool isVert) => door.name == "door_vault" ? new(-0.082f, 0.56f, -0.01f) : isVert ? new(-0.005f, 0.56f, -0.01f) : new(0f, -0.1f, -0.01f);

    public override WindType GetWindType(Vector2 position)
    {
        if (position.y < -14.08f) return WindType.AirshipOutside;
        if (25.26f < position.x && position.x < 29.66f && -3.7f < position.y && position.y < 2.4f) return WindType.AirshipVentilation;
        if (2.9f < position.x && position.x < 16.1f && 5.7f < position.y && position.y < 11.9f) return WindType.AirshipGapRoom;
        return WindType.NoWind;
    }
}

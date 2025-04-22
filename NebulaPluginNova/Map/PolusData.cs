using Virial.Game;

namespace Nebula.Map;

public class PolusData : MapData
{
    static private Vector2[] MapPositions = [
        //ドロップシップ
        new(16.7f, -2.6f),
        //ドロップシップ下
        new(14.1f, -10f), new(22.0f, -7.1f),
        //エレクトリカル
        new(7.5f, -9.7f), new(3.1f, -11.7f), new(5.4f, -11.5f), new(9.6f, -12.1f),
        //O2
        new(4.7f, -19f), new(2.4f, -17f), new(3.1f, -21.7f), new(1.9f, -19.4f), new(2.4f, -23.6f), new(6.3f, -21.3f),
        //Elec,O2,Comm周辺外
        new(7.9f, -23.6f), new(9.4f, -20.1f), new(8.2f, -16.0f), new(8.0f, -14.3f), new(13.4f, -13f),
        //左上リアクター前通路
        new(10.3f, -7.4f),
        //左上リアクター
        new(4.6f, -5f),
        //Comm
        new(11.4f, -15.9f), new(11.7f, -17.3f),
        //Weapons
        new(13f, -23.5f),
        //Storage
        new(19.4f, -11.2f),
        //オフィス左下
        new(18f, -24.5f),new(14.5f, -24.2f),
        //オフィス
        new(18.6f, -21.5f), new(20.2f, -19.2f), new(19.6f, -17.6f), new(19.6f, -16.4f), new(26.5f, -17.4f),new(17.3f,-18.6f),new(22.3f,-18.6f),
        //アドミン
        new(20f, -22.5f), new(21.4f, -25.2f), new(22.4f, -22.6f), new(25f, -20.8f),
        //デコン（左）
        new(24.1f, -24.7f),
        //スペシメン左通路
        new(27.7f, -24.7f), new(33f, -20.6f),
        //スペシメン
        new(36.8f, -21.6f), new(36.5f, -19.3f),
        //スペシメン右通路
        new(39.2f, -15.2f),
        //デコン(上)
        new(39.8f, -10f),
        //ラボ
        new(34.7f, -10.2f), new(36.4f, -8f), new(40.5f, -7.6f), new(34.5f, -6.2f), new(31.2f, -7.6f), new(28.4f, -9.6f), new(26.5f, -7f), new(26.5f, -8.3f),new(33.0f, -9.7f),
        //右リアクター
        new(24.2f, -4.5f),
        //ストレージ・ラボ下・オフィス右
        new(24f, -14.6f), new(26f, -12.2f), new(29.8f, -15.7f)
    ];

    static private (AdditionalRoomArea area, string key, bool detailRoom)[] additionalRooms = [
        (new(16.61f, -7.10f, 5.44f, 1.69f), "nearDropship", true),
        (new(14.88f, -10.97f, 4.15f, 4.90f), "snowdrift", true),
        (new(5.09f, -16.10f, 1.20f, 3.26f), "lifeSupportToElectrical", true),
        (new(8.25f, -16.69f, 2.14f, 3.14f), "commLeft", true),
        (new(6.96f, -24.03f, 3.25f, 2.12f), "lifesupportLower", true),
        (new(26.64f, -13.68f, 2.98f, 1.80f), "abditory", true),
        (new(23.98f, -3.57f, 1.81f, 2.56f), "reactorRight", true),
        (new(4.39f, -4.84f, 1.55f, 2.43f), "reactorLeft", true),
        (new(20.62f, -18.98f, 4.91f, 1.41f),"officeBuilding", false),
        (new(18.84f, -21.07f, 1.30f, 1.93f),"officeBuilding", false),
        (new(0f,0f,1000f,1000f), "outside", false),
        ];
    static private (SystemTypes room, AdditionalRoomArea area, string key)[] overrideRooms = [
        (SystemTypes.Electrical, new(9.10f, -12.46f, 2.60f, 1.60f), "breaker"),
        (SystemTypes.Decontamination3, new(29.86f, -22.75f, 4.96f, 3.71f), "specimenLeft"),
        (SystemTypes.Decontamination2, new(38.81f, -16.17f, 1.72f, 4.34f), "specimenRight"),
        ];

    static private MapObjectPoint[] mapObjectPoints = [
        new(18.8f, -1.0f, MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ右上
        new(16.7f, -2.2f, MapObjectType.Reachable), //ドロップシップ中央
        new(18.5f, -5.6f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ドロップシップ右下
        new(14.7f, -3.9f,MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ左下
        new(20.6f, -7.9f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ドロップシップ右下外
        new(21.3f, -6.7f, MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ右下給油横
        new(3.7f, -7.5f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //左リアクター下
        new(7.0f, -13.1f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //配電盤
        new(3.6f, -11.4f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //セキュリティ
        new(4.9f, -16.7f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //O2上通路
        new(1.2f, -17.5f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //O2グリーン
        new(0.6f, -15.7f, MapObjectType.SmallOrTabletopOutOfSight),//O2グリーン左上
        new(1.9f, -20.3f,MapObjectType.SmallInCorner), //O2缶タスク
        new(1.2f, -18.7f, MapObjectType.SmallOrTabletopOutOfSight), //O2缶タスク上
        new(0.6f, -21.8f, MapObjectType.Reachable), //O2缶タスク前
        new(2.3f, -24.5f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ボイラー
        new(5.0f, -25.0f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //O2,Weapon間左
        new(8.8f, -25.4f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //O2,Weapon間右
        new(9.5f, -17.1f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //コミュ左
        new(9.5f, -15.9f, MapObjectType.SmallOrTabletopOutOfSight), //コミュ左、上方
        new(6.7f, -17.3f, MapObjectType.SmallOrTabletopOutOfSight), //コミュ左O2寄り
        new(12.7f, -17.4f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //コミュ中
        new(14.2f, -21.2f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //武器庫上
        new(12.5f, -24.6f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //武器庫中
        new(17.9f, -13.2f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ストレージ下
        new(19.9f, -10.8f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ストレージ中
        new(19.8f, -12.4f, MapObjectType.SmallOrTabletopOutOfSight), //ストレージ左下
        new(25.2f, -7.8f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボドリル
        new(28.1f, -7.4f, MapObjectType.SmallOrTabletopOutOfSight), //ラボドリル右
        new(32.1f, -10.0f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ラボ中央左
        new(29.6f, -8.2f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボドリル右の部屋
        new(34.9f, -5.5f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //望遠鏡
        new(40.6f, -8.0f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ラボ右端
        new(34.9f, -10.4f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボトイレ
        new(26.3f, -14.1f,MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //大岩前
        new(32.0f, -13.5f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //溶岩湖上
        new(30.9f, -17.2f,MapObjectType.SmallInCorner | MapObjectType.Reachable), //オフィス右
        new(18.7f, -18.7f, MapObjectType.SmallInCorner), //会議室
        new(21.6f, -16.3f, MapObjectType.SmallOrTabletopOutOfSight), //会議室右上
        new(16.9f, -25.9f,MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //オフィス左下
        new(22.2f, -25.2f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //アドミン下
        new(22.4f, -20.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //アドミン上
        new(25.0f, -25.2f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //下除染
        new(28.1f, -24.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //スペシメン左通路下
        new(27.3f, -20.5f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //スペシメン左通路上
        new(35.1f, -22.0f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //スペシメン左下
        new(37.4f, -22.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //スペシメン右下
        new(39.4f, -18.5f, MapObjectType.SmallInCorner), //スペシメン上通路下
        new(40.6f, -10.4f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //上除染
    ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => [];
    protected override (AdditionalRoomArea area, string key, bool detailRoom)[] AdditionalRooms => additionalRooms;
    protected override (SystemTypes room, AdditionalRoomArea area, string key)[] OverrideRooms => overrideRooms;
    protected override SystemTypes[] SabotageTypes => new SystemTypes[] { SystemTypes.Laboratory, SystemTypes.Comms, SystemTypes.Electrical };

    private static IDividedSpriteLoader SealedVentSpritePolus = DividedSpriteLoader.FromResource("Nebula.Resources.Sealed.SealedVentPolus.png", 100f, 8, 2);
    protected override IDividedSpriteLoader SealedVentSprite => SealedVentSpritePolus;
    override public bool IsSealableDoor(OpenableDoor door) => !(door.name is "DeconDoorOuter" or "DeconDoorInner");
}

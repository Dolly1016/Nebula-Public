using Virial.Game;

namespace Nebula.Map;

public class MiraData : MapData
{
    static private Vector2[] MapPositions = new Vector2[]
    {
        //ラウンチパッド
        new(-4.4f, 3.3f),
        //ランチパッド下通路
        new(3.7f, -1.7f),
        //メッドベイ
        new(15.2f, 0.4f),
        //コミュ
        new(14f, 4f),
        //三叉路
        new(12.3f, 6.7f), new(23.6f, 6.8f),
        //ロッカー
        new(9f, 5f), new(8.4f, 1.4f),
        //デコン
        new(6.0f, 5.6f),
        //デコン上通路
        new(6.0f, 11.6f),
        //リアクター
        new(2.5f, 10.3f), new(2.5f, 13f),
        //ラボラトリ
        new(7.6f, 13.9f), new(9.7f, 10.4f), new(10.7f, 12.2f),
        //カフェ
        new(21.8f, 5f), new(10.7f, 12.2f), new(28.3f, 0.2f), new(25.5f, 2.3f), new(22.1f, 2.6f),
        //ストレージ
        new(19.2f, 1.7f), new(18.5f, 4.2f),
        //バルコニー
        new(18.3f, -3.2f), new(23.7f, -1.9f),
        //三叉路上通路
        new(17.8f, 19f),
        //オフィス
        new(15.7f, 17.2f), new(13.7f, 20.4f), new(13.6f, 18.7f),
        //アドミン
        new(20.6f, 20.8f), new(22.3f, 18.6f), new(21.2f, 17.3f), new(19.4f, 17.6f),
        //グリーンハウス
        new(13.2f, 22.3f), new(22.4f, 23.3f), new(20.2f, 24.3f), new(16.5f, 24.4f), new(20.7f, 22.2f), new(18f, 25.3f),
    };

    static private (AdditionalRoomArea area, string key, bool detailRoom)[] additionalRooms = [];
    static private (SystemTypes room, AdditionalRoomArea area, string key)[] overrideRooms = [];

    static private MapObjectPoint[] mapObjectPoints = [
        new(-3.0f, 3.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ右上
        new(-5.7f, -2.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ下外
        new(1.4f, -0.9f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //ドロップシップ下通路右方
        new(8.5f, -1.9f,MapObjectType.SmallOrTabletopOutOfSight), //メッドベイ、O2復旧コンソール間
        new(13.2f, -1.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //メッドベイ左外
        new(16.7f, -1.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //メッドベイ右下
        new(14.1f, 2.8f, MapObjectType.SmallInCorner | MapObjectType.Reachable| MapObjectType.SmallOrTabletopOutOfSight), //コミュ左下
        new(16.3f, 2.8f, MapObjectType.SmallOrTabletopOutOfSight), //コミュ右下
        new(10.8f, 5.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ロッカー右上
        new(9.2f, 1.2f, MapObjectType.Reachable), //ロッカー中央
        new(5.4f, 5.7f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //除染中央
        new(4.5f, 12.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //リアクター右
        new(11.0f, 12.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable), //ラボ右
        new(11.8f, 10.0f, MapObjectType.SmallOrTabletopOutOfSight), //ラボ右下隅
        new(16.0f, 10.9f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //三叉路中央左下
        new(20.6f, 9.6f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //三叉路中央右下
        new(18.5f, 13.7f, MapObjectType.SmallInCorner), //三叉路中央上
        new(17.8f, 11.5f, MapObjectType.Reachable), //三叉路中央
        new(14.1f, 20.9f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //オフィス配電盤そば
        new(13.4f, 17.2f, MapObjectType.SmallInCorner), //オフィス下
        new(17.1f, 18.6f, MapObjectType.Reachable), //オフィス前
        new(19.5f, 20.6f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //アドミン左上
        new(22.4f, 19.7f, MapObjectType.SmallOrTabletopOutOfSight), //アドミン右上
        new(19.5f, 23.9f, MapObjectType.SmallInCorner), //グリーンハウス右上
        new(22.1f, 23.2f, MapObjectType.SmallOrTabletopOutOfSight), //グリーンハウス右端
        new(13.7f, 24.0f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //グリーンハウス左
        new(19.0f, 25.3f, MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight),//グリーンハウス上ベント横
        new(26.3f, 5.4f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //カフェ上
        new(28.3f, -0.1f, MapObjectType.SmallInCorner | MapObjectType.Reachable | MapObjectType.SmallOrTabletopOutOfSight), //カフェ右下
        new(23.7f, 0.0f, MapObjectType.Reachable), //カフェ下
        new(21.6f, -2.3f, MapObjectType.SmallInCorner | MapObjectType.SmallOrTabletopOutOfSight), //カフェ下展望
        new(20.2f, 2.0f, MapObjectType.SmallInCorner), //ストレージ下
        new(19.8f, 4.1f, MapObjectType.SmallOrTabletopOutOfSight), //ストレージ中
        ];
    public override MapObjectPoint[] MapObjectPoints => mapObjectPoints;
    protected override Vector2[] MapArea => MapPositions;
    protected override Vector2[] NonMapArea => [];
    protected override (AdditionalRoomArea area, string key, bool detailRoom)[] AdditionalRooms => additionalRooms;
    protected override (SystemTypes room, AdditionalRoomArea area, string key)[] OverrideRooms => overrideRooms;
    protected override SystemTypes[] SabotageTypes => new SystemTypes[] { SystemTypes.Reactor, SystemTypes.Comms, SystemTypes.Electrical, SystemTypes.LifeSupp };
    protected override IDividedSpriteLoader SealedVentSprite => SkeldData.SealedVentSpriteSkeld;
    private HashSet<string> altSealedVents = ["LaunchVent", "ReactorVent"];
    public override Sprite GetSealedVentSprite(Vent vent, int level, bool remove)
    {
        return base.GetSealedVentSprite(vent, altSealedVents.Contains(vent.name) ? (8 + level) : level, remove);
    }
    override public bool IsSealableDoor(OpenableDoor door) => false;
}

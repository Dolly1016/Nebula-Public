using System;
using System.Collections.Generic;
using System.Linq;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Game.Statistics;

/// <summary>
/// マップ上に現れた物を 1 つ追いかける。
/// </summary>
/// <remarks>
/// 呼び出し側はこれを持っておき、消えるときに <see cref="Despawn"/> を呼ぶだけでよい。
/// 動く物の場合、居場所は <see cref="MapObjectRecorder"/> が一定の間隔で見に来る。
/// </remarks>
internal sealed class MapObjectTracker
{
    public int Id { get; }
    public MapObjectKind Kind { get; }
    public bool IsAlive => despawnTime == null;

    private readonly float spawnTime;
    private readonly VVector2 origin;
    private readonly VVector2 velocity;
    private readonly float angle;
    private readonly bool flipX;
    private readonly bool flipY;

    /// <summary>今の居場所を知る手立て。動かない物では null。</summary>
    private readonly Func<VVector2>? source;

    private readonly List<ArchivedMapObjectMove> moves = [];
    private VVector2 lastRecorded;
    private float? despawnTime = null;

    private readonly int initialState;
    private int state;
    private readonly List<ArchivedMapObjectChange> changes = [];

    /// <summary>持ち主。付いて回る物でだけ使う。</summary>
    private readonly byte? ownerId;

    internal MapObjectTracker(int id, MapObjectKind kind, float time, VVector2 position, VVector2 velocity, float angle, bool flipX, bool flipY, int state, byte? ownerId, Func<VVector2>? source)
    {
        Id = id;
        Kind = kind;

        this.spawnTime = time;
        this.origin = position;
        this.velocity = velocity;
        this.angle = angle;
        this.flipX = flipX;
        this.flipY = flipY;
        this.initialState = state;
        this.state = state;
        this.ownerId = ownerId;
        this.source = kind.Has<MapObjectTrait.Movable>() ? source : null;
        this.lastRecorded = position;
    }

    /// <summary>周りから見えているかどうかを書き換える。</summary>
    public void SetHidden(bool hidden)
    {
        if (!Kind.Has<MapObjectTrait.Concealable>()) return;
        Apply(ArchivedMapObjectState.Of(hidden, ArchivedMapObjectState.StageOf(state)));
    }

    /// <summary>何枚目の絵で描くかを書き換える。</summary>
    /// <remarks>持っている絵の枚数を超える段は受け付けない。</remarks>
    public void SetStage(int stage)
    {
        if (stage < 0 || stage >= Kind.Icons.Count) return;
        Apply(ArchivedMapObjectState.Of(ArchivedMapObjectState.IsHidden(state), stage));
    }

    /// <summary>
    /// ありさまを書き換える。変わらなかったときは記録を増やさない。
    /// </summary>
    private void Apply(int next)
    {
        if (!IsAlive) return;
        if (next == state) return;

        state = next;
        changes.Add(new ArchivedMapObjectChange(Round(Now), state));
    }

    /// <summary>この物が消えたことを記録する。</summary>
    public void Despawn()
    {
        if (!IsAlive) return;
        despawnTime = Now;
    }

    /// <summary>
    /// 今の居場所を見に行き、前より離れていれば記録する。
    /// </summary>
    internal void Sample(float time)
    {
        if (source == null) return;
        Record(time, source());
    }

    /// <summary>
    /// 今の居場所を外から知らせる。
    /// </summary>
    /// <remarks>
    /// 手元では居場所が分からない物のためのもの。持ち主だけが知っている物はこちらで記録する。
    /// </remarks>
    public void Report(VVector2 position) => Record(Now, position);

    private void Record(float time, VVector2 position)
    {
        if (!IsAlive) return;

        var threshold = Kind.Get<MapObjectTrait.Movable>()?.Threshold ?? 0f;
        if (position.Distance(lastRecorded) < threshold) return;

        moves.Add(new ArchivedMapObjectMove(Round(time), Round(position.x), Round(position.y)));
        lastRecorded = position;
    }

    internal ArchivedMapObject ToArchive() => new(
        Id, Kind.Id, spawnTime, despawnTime,
        Round(origin.x), Round(origin.y),
        Round(velocity.x), Round(velocity.y),
        angle, flipX, flipY, moves,
        initialState, changes, ownerId);

    static internal float Now => NebulaGameManager.Instance?.CurrentTime ?? 0f;

    //足取りと同じく小数第1位まで。
    static private float Round(float value) => (float)Math.Round(value, 1);
}

/// <summary>
/// マップ上に現れた物を記録する。
/// </summary>
/// <remarks>
/// 居場所を見に行く間隔は足取りと同じで、<see cref="MovementRecorder"/> から呼ばれる。
/// 止まっている間は記録が増えないので、動かない物は現れた 1 点だけで済む。
/// </remarks>
[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class MapObjectRecorder : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess) =>
        DIManager.Instance.RegisterModule(() => new MapObjectRecorder());

    private MapObjectRecorder()
    {
        ModSingleton<MapObjectRecorder>.Instance = this;
        this.RegisterPermanently();
    }

    private const int SharedIdMask = 0x01000000;

    private int nextLocalId = -1;

    private int nextSharedId = 0;
    private readonly List<MapObjectTracker> trackers = [];

    public int IssueSharedId() => SharedIdMask | (AmongUsLLImpl.LocalPlayer.PlayerId << 16) | (nextSharedId++ & 0xFFFF);

    public MapObjectTracker? Find(int id) => trackers.FirstOrDefault(t => t.Id == id);

    /// <summary>ベントの記録。バニラのベント番号で引く。</summary>
    /// <remarks>封鎖の知らせがベント番号で届くので、そこから記録へ辿れるようにしておく。</remarks>
    private readonly Dictionary<int, MapObjectTracker> ventTrackers = [];

    public MapObjectTracker? FindVent(int ventId) => ventTrackers.TryGetValue(ventId, out var found) ? found : null;

    [EventPriority(EventPriority.High)]
    void OnGameStart(GameStartEvent ev)
    {
        var ship = AmongUsLLImpl.ShipStatusInstance;
        if (!ship.AsBoolFast()) return;

        foreach (var vent in ship.AllVents)
        {
            var transform = vent.transform;
            ventTrackers[vent.Id] = Track(MapObjectKinds.Vent, () => (VVector2)transform.position);
        }
    }

    public MapObjectTracker Spawn(MapObjectKind kind, VVector2 position, VVector2 velocity = default, float angle = 0f, bool flipX = false, bool flipY = false, bool hidden = false, int? id = null, byte? ownerId = null) =>
        Add(kind, position, velocity, angle, flipX, flipY, hidden, id, ownerId, null);

    public MapObjectTracker Track(MapObjectKind kind, Func<VVector2> source, float angle = 0f, bool flipX = false, bool flipY = false, bool hidden = false, int? id = null) =>
        Add(kind, source(), default, angle, flipX, flipY, hidden, id, null, source);

    private MapObjectTracker Add(MapObjectKind kind, VVector2 position, VVector2 velocity, float angle, bool flipX, bool flipY, bool hidden, int? id, byte? ownerId, Func<VVector2>? source)
    {
        var state = ArchivedMapObjectState.Of(hidden, 0);
        var tracker = new MapObjectTracker(id ?? nextLocalId--, kind, MapObjectTracker.Now, position, velocity, angle, flipX, flipY, state, ownerId, source);
        trackers.Add(tracker);
        return tracker;
    }

    internal IReadOnlyList<int> AliveIds => [.. trackers.Where(t => t.IsAlive).Select(t => t.Id)];

    internal void Sample(float time)
    {
        foreach (var tracker in trackers) tracker.Sample(time);
    }

    /// <summary>
    /// ターンを跨がない物を畳む。ターンの終わりに呼ぶ。
    /// </summary>
    /// <remarks>
    /// 会議が始まれば必ず無くなる物は、消えたことを知らせてもらわなくてよい。
    /// </remarks>
    internal void DropTransients()
    {
        foreach (var tracker in trackers)
        {
            if (tracker.Kind.Has<MapObjectTrait.WithinTurn>()) tracker.Despawn();
        }
    }

    public IReadOnlyList<ArchivedMapObject> ToArchive() => [.. trackers.Select(t => t.ToArchive())];
}

using System.Collections.Generic;
using System.Linq;

namespace Nebula.Game.Statistics;

/// <summary>
/// マップオブジェクトの性質。
/// </summary>
/// <remarks>
/// 記録の仕方も見せ方も、種類で場合分けせずこの性質から決める。
/// 新しい物を足すときは <see cref="MapObjectKinds"/> に性質を並べるだけでよい。
/// </remarks>
internal abstract record MapObjectTrait
{
    /// <summary>
    /// 位置が変わりうる。
    /// </summary>
    /// <param name="Threshold">
    /// 最後に記録した場所からこれだけ離れて初めて記録し直す。
    /// 見に行く間隔は足取りと同じなので、止まっている間の記録が積み上がらないようにする。
    /// </param>
    internal sealed record Movable(float Threshold) : MapObjectTrait;

    /// <summary>消えうる。消えた時刻を記録する。</summary>
    internal sealed record Despawnable : MapObjectTrait;

    /// <summary>
    /// 等速直線運動をする。
    /// </summary>
    /// <remarks>
    /// 現れたときの位置と速度さえ残せば、以後の場所は計算で出る。道中は一切記録しない。
    /// </remarks>
    internal sealed record LinearMotion : MapObjectTrait;

    /// <summary>
    /// 持ち主にぴったり付いて回る。
    /// </summary>
    /// <remarks>
    /// 居場所は持ち主の足取りそのものなので、この物の座標は1点も記録しない。
    /// </remarks>
    internal sealed record FollowsOwner : MapObjectTrait;

    /// <summary>向きを持つ。現れたときの角度を記録する。角度0は右向き。</summary>
    internal sealed record Oriented : MapObjectTrait;

    /// <summary>見せるとき絶えず回す。記録には関わらない。</summary>
    /// <param name="TurnsPerSecond">1 秒あたりの回転数。</param>
    internal sealed record Spinning(float TurnsPerSecond) : MapObjectTrait;

    /// <summary>
    /// 周りから見えている時と見えていない時がある。
    /// </summary>
    /// <remarks>
    /// 置いた本人にしか見えていない間も居場所は記録する。見え方で区別が付くようにする。
    /// </remarks>
    internal sealed record Concealable : MapObjectTrait;

    /// <summary>
    /// 絵が複数枚あり、段で使い分ける。
    /// </summary>
    /// <remarks>
    /// <see cref="MapObjectKind.IconIndex"/> が段0の絵で、ここに並べた番号が段1以降。
    /// 絵の番号は連続していなくてよい。
    /// </remarks>
    internal sealed record Staged(params int[] Icons) : MapObjectTrait;

    /// <summary>
    /// ターンを跨がない。
    /// </summary>
    /// <remarks>
    /// 会議が始まれば必ず無くなる物。消えたことをわざわざ知らせなくても、
    /// ターンの終わりに記録の方で畳む。
    /// </remarks>
    internal sealed record WithinTurn : MapObjectTrait;
}

/// <summary>
/// マップオブジェクトの種類。
/// </summary>
/// <param name="Id">記録に残す名前。</param>
/// <param name="IconIndex">MapViewerIcons.png の何番のマスか。左上から順に 0, 1, …。</param>
internal sealed record MapObjectKind(string Id, int IconIndex, params MapObjectTrait[] Traits)
{
    public bool Has<T>() where T : MapObjectTrait => Traits.Any(t => t is T);
    public T? Get<T>() where T : MapObjectTrait => Traits.OfType<T>().FirstOrDefault();

    /// <summary>段ごとの絵の番号。段を持たない種類では1つだけ。</summary>
    public IReadOnlyList<int> Icons => [IconIndex, .. Get<MapObjectTrait.Staged>()?.Icons ?? []];
}

/// <summary>
/// 閲覧画面に出すマップオブジェクトの一覧。
/// </summary>
internal static class MapObjectKinds
{
    /// <summary>動く物を記録し直す距離。</summary>
    private const float MoveThreshold = 0.1f;

    /// <summary>ベント。テープを貼られ(29)、会議を跨ぐと封鎖される(28)。</summary>
    public static readonly MapObjectKind Vent = new("vent", 0,
        new MapObjectTrait.Movable(MoveThreshold), new MapObjectTrait.Staged(29, 28));

    public static readonly MapObjectKind Bubble = new("bubble", 1,
        new MapObjectTrait.LinearMotion(), new MapObjectTrait.Despawnable());

    public static readonly MapObjectKind FlyingAxe = new("flyingAxe", 2,
        new MapObjectTrait.LinearMotion(), new MapObjectTrait.Despawnable(), new MapObjectTrait.Spinning(6f));

    public static readonly MapObjectKind StuckAxe = new("stuckAxe", 3, new MapObjectTrait.Oriented());

    /// <summary>テレポータは 4 種類あり、アイコンも 4 つ並んでいる。</summary>
    public static readonly MapObjectKind[] Teleporters =
        [.. Enumerable.Range(0, 4).Select(kind => new MapObjectKind("teleporter" + kind, 4 + kind))];

    public static readonly MapObjectKind Decoy = new("decoy", 8,
        new MapObjectTrait.Movable(MoveThreshold), new MapObjectTrait.Despawnable());

    public static readonly MapObjectKind Medkit = new("medkit", 9);

    /// <summary>飛ばしている間のドローン。本人にしか見えず、会議を跨がない。</summary>
    public static readonly MapObjectKind Drone = new("drone", 10,
        new MapObjectTrait.Movable(MoveThreshold), new MapObjectTrait.Despawnable(),
        new MapObjectTrait.Concealable(), new MapObjectTrait.WithinTurn());

    /// <summary>切り離した後のドローン。その場に残り、周りにも見える。飛ばしていた物とは別に数える。</summary>
    public static readonly MapObjectKind DetachedDrone = new("detachedDrone", 10);

    /// <summary>置いた直後は本人にしか見えない。絵は消灯(11)と点灯(12)の2枚。</summary>
    public static readonly MapObjectKind Lantern = new("lantern", 11,
        new MapObjectTrait.Concealable(), new MapObjectTrait.Staged(12));

    /// <summary>香炉。活性化している間だけ絵が変わる。</summary>
    public static readonly MapObjectKind Censer = new("censer", 13, new MapObjectTrait.Staged(14));

    /// <summary>おやすみボム。インポスター以外には終始見えない。</summary>
    public static readonly MapObjectKind NightyBomb = new("nightyBomb", 15,
        new MapObjectTrait.Despawnable(), new MapObjectTrait.Concealable());

    /// <summary>夜のランタン。置いた直後は本人だけが知っている。</summary>
    public static readonly MapObjectKind NightmareSeed = new("nightmareSeed", 16,
        new MapObjectTrait.Concealable(), new MapObjectTrait.Staged(17), new MapObjectTrait.Despawnable());

    /// <summary>加速トラップ。会議で全体に公開される。</summary>
    public static readonly MapObjectKind AccelTrap = new("accelTrap", 18, new MapObjectTrait.Concealable());

    /// <summary>減速トラップ。会議で全体に公開される。</summary>
    public static readonly MapObjectKind DecelTrap = new("decelTrap", 19, new MapObjectTrait.Concealable());

    /// <summary>コミュトラップ。置いた本人以外には終始見えない。</summary>
    public static readonly MapObjectKind CommTrap = new("commTrap", 20, new MapObjectTrait.Concealable());

    /// <summary>キルトラップ。使われるまで本人以外には見えない。使うと絵が変わる(22)。</summary>
    public static readonly MapObjectKind KillTrap = new("killTrap", 21,
        new MapObjectTrait.Concealable(), new MapObjectTrait.Staged(22));

    /// <summary>トラップの種類。<c>Trapper.Trap.TypeId</c> の並びに揃えている。</summary>
    public static readonly MapObjectKind[] Traps = [AccelTrap, DecelTrap, CommTrap, KillTrap];

    /// <summary>ドリル。持ち主に付いて回り、発進した向きのまま進む。</summary>
    public static readonly MapObjectKind Drill = new("drill", 23,
        new MapObjectTrait.FollowsOwner(), new MapObjectTrait.Oriented(),
        new MapObjectTrait.Despawnable(), new MapObjectTrait.WithinTurn());

    /// <summary>妖狐の皿。お揚げがある(24)か、無い(25)か。</summary>
    public static readonly MapObjectKind SpectreDish = new("spectreDish", 24, new MapObjectTrait.Staged(25));

    /// <summary>ウツボカズラ。毒を蓄えている間は絵が変わる(27)。</summary>
    public static readonly MapObjectKind PoisonPod = new("poisonPod", 26, new MapObjectTrait.Staged(27));

    /// <summary>ドアに貼ったテープ。会議を跨ぐと封鎖に変わるので、ここまでしか残らない。</summary>
    public static readonly MapObjectKind SealedDoor = new("sealedDoor", 30,
        new MapObjectTrait.WithinTurn(), new MapObjectTrait.Despawnable());

    /// <summary>パークプラント。4段階に育つ。</summary>
    public static readonly MapObjectKind PerkPlant = new("perkPlant", 32, new MapObjectTrait.Staged(33, 34, 35));

    /// <summary>人外専用のパークプラント。</summary>
    public static readonly MapObjectKind PerkPlantNoncrewmate = new("perkPlantNoncrewmate", 36,
        new MapObjectTrait.Staged(37, 38, 39));

    public static readonly MapObjectKind SlingshotConsole = new("slingshotConsole", 40);

    /// <summary>風船を仕掛けたコンソール。仕掛けた本人にしか見えない。</summary>
    public static readonly MapObjectKind BalloonTrap = new("balloonTrap", 41,
        new MapObjectTrait.Despawnable(), new MapObjectTrait.Concealable());

    public static IEnumerable<MapObjectKind> All
    {
        get
        {
            yield return Vent;
            yield return Bubble;
            yield return FlyingAxe;
            yield return StuckAxe;
            foreach (var teleporter in Teleporters) yield return teleporter;
            yield return Decoy;
            yield return Medkit;
            yield return Drone;
            yield return DetachedDrone;
            yield return Lantern;
            yield return Censer;
            yield return NightyBomb;
            yield return NightmareSeed;
            foreach (var trap in Traps) yield return trap;
            yield return Drill;
            yield return SpectreDish;
            yield return PoisonPod;
            yield return SealedDoor;
            yield return PerkPlant;
            yield return PerkPlantNoncrewmate;
            yield return SlingshotConsole;
            yield return BalloonTrap;
        }
    }
}

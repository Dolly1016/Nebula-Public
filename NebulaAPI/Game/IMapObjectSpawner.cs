using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;

namespace Virial.Game;

[Flags]
public enum MapObjectType
{
    /// <summary>
    /// 小物を置くのに適した、奥まった場所
    /// </summary>
    SmallInCorner = 0x0001,
    /// <summary>
    /// 小物を置くのに適した、開けた場所
    /// </summary>
    SmallOpened = 0x0002,
    /// <summary>
    /// 容易に到達可能な地点
    /// </summary>
    Reachable = 0x0004,
    /// <summary>
    /// 視界に入りづらい机の上、あるいは小狭い場所
    /// </summary>
    SmallOrTabletopOutOfSight = 0x0008,
    /// <summary>
    /// 医療キット
    /// </summary>
    DepoisonBox = 0x0010,

}

public record MapObjectCondition(string Tag, float Distance);

public interface IMapObjectSpawner : IModule
{
    /// <summary>
    /// 指定の個数だけマップオブジェクトを生成する先を提案させます。
    /// 提案のたびに状態が変化します。不必要な提案をさせないでください。
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    Virial.Compat.Vector2[] Spawn(int num, float distance, string reason, string? objectConstructor, MapObjectType type, MapObjectCondition[]? conditions = null);

    /// <summary>
    /// 同期のためのSpawnメソッド
    /// </summary>
    /// <param name="id"></param>
    /// <param name="reason"></param>
    void Spawn(int id, string reason);
}

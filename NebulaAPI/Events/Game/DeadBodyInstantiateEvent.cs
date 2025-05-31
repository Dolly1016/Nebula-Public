using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// 死体が生成されるときに呼び出されます。
/// </summary>
public class DeadBodyInstantiateEvent : Event
{
    /// <summary>
    /// 死亡したプレイヤーです。
    /// </summary>
    public Virial.Game.Player? Player { get; private init; }
    internal DeadBody DeadBody { get; private init; }

    internal DeadBodyInstantiateEvent(Virial.Game.Player? player, DeadBody deadBody)
    {
        Player = player;
        DeadBody = deadBody;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Text;

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
    public Virial.Game.DeadBody DeadBody { get; private init; }
    public Virial.Game.Player? Killer { get; }
    public CommunicableTextTag DeadState { get; }

    internal DeadBodyInstantiateEvent(Virial.Game.Player? player, Virial.Game.DeadBody deadBody, Virial.Game.Player? killer, CommunicableTextTag playerState)
    {
        Player = player;
        DeadBody = deadBody;
        Killer = killer;
        DeadState = playerState;
    }
}
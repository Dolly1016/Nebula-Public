using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーが切断したときに呼び出されます。
/// </summary>
public class PlayerDisconnectEvent : PlayerDieOrDisconnectEvent
{
    internal PlayerDisconnectEvent(Virial.Game.Player player) : base(player) { }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーがいずれかのベントから出た際に呼び出されます。
/// </summary>
public class PlayerExitVentEvent : AbstractPlayerEvent
{
    internal PlayerExitVentEvent(Game.Player player) : base(player)
    {
    }
}


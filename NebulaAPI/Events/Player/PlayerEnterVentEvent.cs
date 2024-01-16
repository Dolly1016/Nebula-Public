using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーがいずれかのベントに入った際に呼び出されます。
/// </summary>
public class PlayerEnterVentEvent : AbstractPlayerEvent
{
    internal PlayerEnterVentEvent(Game.Player player) : base(player)
    {
    }
}


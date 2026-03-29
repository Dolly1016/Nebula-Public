using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Door;

/// <summary>
/// プレイヤーがドアを開けようとしたときに発火するイベントです。
/// </summary>
public class PlayerTryOpenDoorLocalEvent : AbstractPlayerEvent
{
    public Virial.Game.Object.Door Door { get; }
    public bool IsCanceled { get; set; } = false;
    internal PlayerTryOpenDoorLocalEvent(Virial.Game.Player player, OpenableDoor door) : base(player)
    {
        this.Door = new(door);
    }
}

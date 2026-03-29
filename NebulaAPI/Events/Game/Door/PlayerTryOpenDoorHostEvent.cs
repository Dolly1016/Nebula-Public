using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Door;


/// <summary>
/// プレイヤーがドアを開けようとしたときにホスト視点でのみ発火するイベントです。
/// PlayerTryOpenDoorLocalEventがキャンセルされなかった場合にのみ発火します。
/// ここでもイベントがキャンセルされなければ、全クライアントでPlayerOpenDoorEventが発火します。
/// </summary>
public class PlayerTryOpenDoorHostEvent : AbstractPlayerEvent
{
    public Virial.Game.Object.Door Door { get; }
    public bool IsCanceled { get; set; } = false;
    internal PlayerTryOpenDoorHostEvent(Virial.Game.Player player, OpenableDoor door) : base(player)
    {
        this.Door = new(door);
    }
}


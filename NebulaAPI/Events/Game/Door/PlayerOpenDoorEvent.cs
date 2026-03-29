using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Door;

public class PlayerOpenDoorEvent : AbstractPlayerEvent
{
    public Virial.Game.Object.Door Door { get; }

    internal PlayerOpenDoorEvent(Virial.Game.Player player, OpenableDoor door) : base(player)
    {
        this.Door = new(door);
    }
}


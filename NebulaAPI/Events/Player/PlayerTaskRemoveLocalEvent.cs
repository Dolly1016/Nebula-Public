using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerTaskRemoveLocalEvent : AbstractPlayerEvent
{
    internal PlayerTask Task { get; set; }

    internal PlayerTaskRemoveLocalEvent(Virial.Game.Player player, PlayerTask task) : base(player)
    {
        this.Task = task;
    }
}


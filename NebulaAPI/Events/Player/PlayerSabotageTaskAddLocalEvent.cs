using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerSabotageTaskAddLocalEvent : AbstractPlayerEvent
{
    internal PlayerTask SystemTask { get; set; }

    internal PlayerSabotageTaskAddLocalEvent(Virial.Game.Player player, PlayerTask systemTask) : base(player)
    {
        this.SystemTask = systemTask;
    }
}

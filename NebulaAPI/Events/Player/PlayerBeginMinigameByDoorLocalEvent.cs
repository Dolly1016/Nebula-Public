using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerBeginMinigameByDoorLocalEvent : AbstractPlayerEvent
{
    internal DoorConsole Door { get; }
    internal PlayerBeginMinigameByDoorLocalEvent(Virial.Game.Player player, DoorConsole door) : base(player)
    {
        Door = door;
    }
}

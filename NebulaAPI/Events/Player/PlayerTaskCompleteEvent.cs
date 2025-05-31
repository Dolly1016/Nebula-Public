using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;


public class PlayerTaskCompleteEvent : AbstractPlayerEvent
{
    internal PlayerTaskCompleteEvent(Virial.Game.Player player) : base(player) { }
}

public class PlayerTaskCompleteLocalEvent : AbstractPlayerEvent
{
    internal PlayerTaskCompleteLocalEvent(Virial.Game.Player player) : base(player) { }
}

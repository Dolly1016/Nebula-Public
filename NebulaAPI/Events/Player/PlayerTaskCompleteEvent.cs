using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;


public class PlayerTaskCompleteEvent : AbstractPlayerEvent
{
    public bool IsVirtualTask { get; private init; }
    internal PlayerTaskCompleteEvent(Virial.Game.Player player, bool isVirtualTask) : base(player) {
        this.IsVirtualTask = isVirtualTask;
    }
}

public class PlayerTaskCompleteLocalEvent : AbstractPlayerEvent
{
    internal PlayerTaskCompleteLocalEvent(Virial.Game.Player player) : base(player) { }
}

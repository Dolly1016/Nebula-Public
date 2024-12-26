using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

public class CheckCanPushEmergencyButtonEvent : Event
{
    public bool CanPushButton { get; private set; }
    public string? CannotPushReason { get; private set; } = null;

    public void DenyButton(string? reason)
    {
        CanPushButton = false;
        CannotPushReason ??= reason;
    }

    internal CheckCanPushEmergencyButtonEvent()
    {
        CanPushButton = true;
    }
}

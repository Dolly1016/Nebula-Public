using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// 自身が観戦者モードになっている間、呼び出されます。
/// </summary>
public class UpdateSpectatorEvent : Event
{
    public bool CanZoom { get; set; }
    public bool CanMonitorAlives { get; set; }
    public bool CanSeeRoles { get; set; }

    internal UpdateSpectatorEvent()
    {
        CanZoom = true;
        CanMonitorAlives = true;
        CanSeeRoles = true;
    }
}

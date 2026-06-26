using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Internal;

internal interface AmongUsLL
{
    float VanillaKillCooldown { get; }
    float VanillaKillDistance { get; }
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    byte MapId { get; }
    internal PlayerControl LocalPlayer { get; }
}

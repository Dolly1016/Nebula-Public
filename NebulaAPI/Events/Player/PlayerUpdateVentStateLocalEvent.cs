using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Events.Player;

public class PlayerUpdateVentStateLocalEvent : AbstractPlayerEvent
{
    public bool CanUseVent { get; set; }
    public bool CannotUseVentTemporary { get; set; }
    public bool ShouldShowVentButton => CanUseVent;
    public bool CanUseVentButton => CanUseVent && !CannotUseVentTemporary;
    internal PlayerUpdateVentStateLocalEvent(Virial.Game.Player player) : base(player)
    {
        CanUseVent = player.Role.CanUseVent;
        CannotUseVentTemporary = false;
    }
}

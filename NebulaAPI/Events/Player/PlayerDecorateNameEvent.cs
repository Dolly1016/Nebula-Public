using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerDecorateNameEvent : AbstractPlayerEvent
{
    public string Name { get; set; }
    public Virial.Color? Color { get; set; } = null;
    public bool CanSeeAllInfo { get; }

    internal PlayerDecorateNameEvent(Virial.Game.Player player, string name, bool canSeeAllInfo) : base(player)
    {
        Name = name;
        CanSeeAllInfo = canSeeAllInfo;
    }
}

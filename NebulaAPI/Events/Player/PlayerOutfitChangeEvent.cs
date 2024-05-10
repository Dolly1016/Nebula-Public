using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerOutfitChangeEvent : AbstractPlayerEvent
{
    public Virial.Game.Outfit Outfit { get; private init; }

    public PlayerOutfitChangeEvent(Virial.Game.Player player, Virial.Game.Outfit outfit) : base(player)
    {
        this.Outfit = outfit;
    }
}

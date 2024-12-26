using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerOutfitChangeEvent : AbstractPlayerEvent
{
    public Virial.Game.OutfitCandidate Outfit { get; private init; }

    public PlayerOutfitChangeEvent(Virial.Game.Player player, Virial.Game.OutfitCandidate outfit) : base(player)
    {
        this.Outfit = outfit;
    }
}

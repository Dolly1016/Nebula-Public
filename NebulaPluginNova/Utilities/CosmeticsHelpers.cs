using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

internal static class CosmeticsHelpers
{
    static public void ChangeBodyTypeAndWrapUp(this GamePlayer player, PlayerBodyTypes bodyType)
    {
        ChangeBodyType(player, bodyType);
        player.VanillaCosmetics.SetBodyCosmeticsVisible(true);
        player.VanillaCosmetics.UpdateVisibility();
    }

    static public void ChangeBodyType(this GamePlayer player, PlayerBodyTypes bodyType)
    {
        var physics = player.VanillaPlayer.MyPhysics;
        var lastFlipX = physics.FlipX;
        physics.SetBodyType(bodyType);
        physics.FlipX = lastFlipX;
    }
}

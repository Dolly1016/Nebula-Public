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
        player.VanillaPlayer.cosmetics.SetBodyCosmeticsVisible(true);
        player.VanillaPlayer.cosmetics.UpdateVisibility();
    }

    static public void ChangeBodyType(this GamePlayer player, PlayerBodyTypes bodyType)
    {
        var lastFlipX = player.VanillaPlayer.MyPhysics.FlipX;
        player.VanillaPlayer.MyPhysics.SetBodyType(bodyType);
        player.VanillaPlayer.MyPhysics.FlipX = lastFlipX;
    }
}

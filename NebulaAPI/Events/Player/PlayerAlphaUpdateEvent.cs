using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerAlphaUpdateEvent : AbstractPlayerEvent
{
    public float Alpha { get; private init; }
    public float AlphaIgnoresWall { get; private init; }

    internal PlayerAlphaUpdateEvent(Virial.Game.Player player, float alpha, float alphaIgnoresWall) : base(player)
    {
        this.Alpha = alpha;
        this.AlphaIgnoresWall = alphaIgnoresWall;
    }
}

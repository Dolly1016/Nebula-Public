using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーの透明度が更新されるときに発火します。
/// </summary>

[RecyclableEvent]
public class PlayerAlphaUpdateEvent : AbstractPlayerEvent
{
    public float Alpha { get; private set; }
    public float AlphaIgnoresWall { get; private set; }

    private PlayerAlphaUpdateEvent() : base(null!)
    {
    }

    static PlayerAlphaUpdateEvent ev = new();
    static internal PlayerAlphaUpdateEvent Get(Virial.Game.Player player, float alpha, float alphaIgnoresWall)
    {
        ev.Recycle(player);
        ev.Alpha = alpha;
        ev.AlphaIgnoresWall = alphaIgnoresWall;
        return ev;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerFixZPositionEvent : AbstractPlayerEvent
{
    public float Y;
    public float? Z = null;
    internal float CalcZ => Z ?? Y / 1000f;
    internal PlayerFixZPositionEvent(Virial.Game.Player player, float y) : base(player)
    {
        this.Y = y;
    }
}

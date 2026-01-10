using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerUseZiplineEvent : AbstractPlayerEvent
{
    public bool GoesToTop { get; set; }
    public Virial.Compat.Vector2 From { get; private init; }
    public Virial.Compat.Vector2 To { get; private init; }
    internal PlayerUseZiplineEvent(Virial.Game.Player player, bool goesToTop, Virial.Compat.Vector2 from, Virial.Compat.Vector2 to) : base(player)
    {
        GoesToTop = goesToTop;
        From = from;
        To = to;
    }

}
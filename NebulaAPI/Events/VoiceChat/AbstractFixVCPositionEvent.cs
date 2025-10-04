using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.VoiceChat;

public abstract class AbstractFixVCPositionEvent : AbstractPlayerEvent
{
    public Virial.Compat.Vector2 Position { get; set; }
    internal AbstractFixVCPositionEvent(Virial.Game.Player player, Virial.Compat.Vector2 position) : base(player)
    {
        this.Position = position;
    }
}

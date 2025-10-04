using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.VoiceChat;

public class FixMicPositionEvent : AbstractFixVCPositionEvent
{
    public bool CanIgnoreWalls { get; set; } = false;
    internal FixMicPositionEvent(Virial.Game.Player player, Virial.Compat.Vector2 position, bool canIgnoreWalls) : base(player, position) { 
        this.CanIgnoreWalls = canIgnoreWalls;
    }
}
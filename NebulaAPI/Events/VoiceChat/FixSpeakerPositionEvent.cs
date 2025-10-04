using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.VoiceChat;

public class FixSpeakerPositionEvent : AbstractFixVCPositionEvent
{
    internal FixSpeakerPositionEvent(Virial.Game.Player player, Virial.Compat.Vector2 position) : base(player, position) { }
}
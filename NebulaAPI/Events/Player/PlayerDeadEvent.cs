using Virial.Game;

namespace Virial.Events.Player;

public class PlayerDeadEvent : AbstractPlayerEvent
{
    internal PlayerDeadEvent(Game.Player dead) : base(dead)
    {
    }
}

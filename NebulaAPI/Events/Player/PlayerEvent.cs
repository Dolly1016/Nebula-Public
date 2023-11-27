using Virial.Game;

namespace Virial.Events.Player;

public abstract class AbstractPlayerEvent
{
    public Game.Player Player { get; internal set; }

    internal AbstractPlayerEvent(Game.Player player)
    {
        Player = player;
    }
}

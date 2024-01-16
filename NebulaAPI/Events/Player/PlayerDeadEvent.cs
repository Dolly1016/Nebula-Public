using Virial.Game;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーが死亡した際に呼び出されます。
/// </summary>
public class PlayerDeadEvent : AbstractPlayerEvent
{
    internal PlayerDeadEvent(Game.Player dead) : base(dead)
    {
    }
}

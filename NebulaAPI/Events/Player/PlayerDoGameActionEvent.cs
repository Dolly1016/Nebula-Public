using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Player;

public class PlayerDoGameActionEvent : AbstractPlayerEvent
{
    public GameActionType ActionType { get; private init; }
    public Virial.Compat.Vector2 Position { get; private init; }
    public PlayerDoGameActionEvent(Virial.Game.Player player, GameActionType actionType, Virial.Compat.Vector2 position): base(player)
    {
        this.ActionType = actionType;
        this.Position = position;
    }
}

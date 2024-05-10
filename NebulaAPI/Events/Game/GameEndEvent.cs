using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

public class GameEndEvent : AbstractGameEvent
{
    public Virial.Game.EndState EndState { get; private init; }
    internal GameEndEvent(Virial.Game.Game game, Virial.Game.EndState endState) : base(game) {
        this.EndState = endState;
    }
}


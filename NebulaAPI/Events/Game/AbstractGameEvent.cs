using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

public class AbstractGameEvent : Event
{
    public Virial.Game.Game Game { get; private set; }

    internal AbstractGameEvent(Virial.Game.Game game)
    {
        this.Game = game;
    }
}

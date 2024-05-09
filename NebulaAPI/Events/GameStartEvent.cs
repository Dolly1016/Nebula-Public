using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events;

public class GameStartEvent : Event
{
    public Game.Game Game { get; private init; }

    internal GameStartEvent(Game.Game game)
    {
        this.Game = game;
    }
}


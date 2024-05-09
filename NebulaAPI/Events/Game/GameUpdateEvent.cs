using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

public class GameUpdateEvent : AbstractGameEvent
{
    internal GameUpdateEvent(Virial.Game.Game game) : base(game) { }
}

public class GameHudUpdateEvent : AbstractGameEvent
{
    internal GameHudUpdateEvent(Virial.Game.Game game) : base(game) { }
}
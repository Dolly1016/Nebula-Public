using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

public class TaskPhaseRestartEvent : TaskPhaseStartEvent
{
    internal TaskPhaseRestartEvent(Virial.Game.Game game) : base(game) { }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// タスクフェイズが再開するときに発火します。
/// </summary>
public class TaskPhaseRestartEvent : TaskPhaseStartEvent
{
    internal TaskPhaseRestartEvent(Virial.Game.Game game) : base(game) { }
}

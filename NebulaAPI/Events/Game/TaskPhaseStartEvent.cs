using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// タスクフェイズが開始あるいは再開するときに発火します。
/// </summary>
public class TaskPhaseStartEvent : AbstractGameEvent
{
    internal TaskPhaseStartEvent(Virial.Game.Game game) : base(game) { }
}


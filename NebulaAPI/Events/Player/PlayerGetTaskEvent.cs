using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerGetTaskLocalEvent : AbstractPlayerEvent
{
    internal NormalPlayerTask Task { get; private init; }
    internal PlayerGetTaskLocalEvent(Virial.Game.Player player, NormalPlayerTask task) : base(player) {
        this.Task = task;
    }
}

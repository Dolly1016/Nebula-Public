using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

internal class UpdateFootprintVisibilityEvent : AbstractGameEvent
{
    public bool Visible { get; set; } = true;
    internal UpdateFootprintVisibilityEvent(Virial.Game.Game game) : base(game) { }
}



﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerTaskCompleteLocalEvent : AbstractPlayerEvent
{
    internal PlayerTaskCompleteLocalEvent(Virial.Game.Player player) : base(player) { }
}

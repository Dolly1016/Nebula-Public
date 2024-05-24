using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerTaskTextLocalEvent : AbstractPlayerEvent
{
    private string text = "";
    public void AppendText(string text) {  this.text += "\n" + text; }
    internal string Text { get => text; }
    internal PlayerTaskTextLocalEvent(Virial.Game.Player player) : base(player) { }
}


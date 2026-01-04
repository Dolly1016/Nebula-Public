using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerTaskTextLocalEvent : AbstractPlayerEvent
{
    private string body = "";
    private string belowText = "";
    public void AppendText(string text) {  this.belowText += "\n" + text; }
    public void ReplaceBody(string text) { this.body = text; }
    internal string Text { get => body + belowText; }
    internal PlayerTaskTextLocalEvent(Virial.Game.Player player, string origText) : base(player) {
        this.body = origText;
    }
}


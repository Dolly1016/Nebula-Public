using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerTaskTextLocalEvent : AbstractPlayerEvent
{
    private string originalBody = "";
    private string? body = null;
    private string belowText = "";
    private Func<string> emergencyText;
    public void AppendText(string text) {  this.belowText += "\n" + text; }
    public void ReplaceBody(string text) { this.body = text; }
    internal string Text { get => (body != null ? body + "<br>" + emergencyText.Invoke() : originalBody) + belowText; }
    internal PlayerTaskTextLocalEvent(Virial.Game.Player player, string origText, Func<string> emergencyText) : base(player) {
        this.originalBody = origText;
        this.emergencyText = emergencyText;
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerSetFakeRoleNameEvent : AbstractPlayerEvent
{
    internal string Text { get; private set; }
    public bool InMeeting { get; private init; }
    internal PlayerSetFakeRoleNameEvent(Virial.Game.Player player, bool inMeeting) : base(player)
    {
        Text = "";
        InMeeting = inMeeting;
    }

    public void Append(string text)
    {
        Text += text;
    }

    public void Set(string text)
    {
        Text = text;
    }
}
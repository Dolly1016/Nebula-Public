using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

internal class PlayerSetFakeRoleNameEvent : AbstractPlayerEvent
{
    internal string AdditionalText { get; private set; }
    internal string? RoleAlternative { get; private set; }
    public bool InMeeting { get; private init; }
    internal PlayerSetFakeRoleNameEvent(Virial.Game.Player player, bool inMeeting) : base(player)
    {
        AdditionalText = "";
        RoleAlternative = null;
        InMeeting = inMeeting;
    }

    public void Append(string text)
    {
        AdditionalText += text;
    }

    public void Alternate(string text)
    {
        RoleAlternative = text;
    }
}
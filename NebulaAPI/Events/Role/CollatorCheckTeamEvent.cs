using MS.Internal.Xml.XPath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Events.Player;
using Virial.Text;

namespace Virial.Events.Role;

/// <summary>
/// Collatorがチームを確認する際に発火します。
/// Collatorのクライアントでのみ発火します。
/// </summary>
/// <remarks>
/// v4.1.0で追加。<br />
/// </remarks>
internal class CollatorCheckTeamEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Target { get; private init; }
    public RoleTeam Team { get; set; }
    internal CollatorCheckTeamEvent(Virial.Game.Player collator, Virial.Game.Player target, RoleTeam team) : base(collator)
    {
        this.Target = target;
        this.Team = team;
    }
}

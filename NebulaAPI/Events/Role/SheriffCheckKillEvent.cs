using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;
using Virial.Events.Player;

namespace Virial.Events.Role;

/// <summary>
/// Sheriffがキルを試みる際に発火します。
/// Sheriff本人のクライアントでのみ発火します。
/// </summary>
/// <remarks>
/// v4.1.0で追加。<br />
/// </remarks>
internal class SheriffCheckKillEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Target { get; private init; }
    public bool CanKill { get; set; }
    internal SheriffCheckKillEvent(Virial.Game.Player sheriff, Virial.Game.Player target, bool canKill) : base(sheriff)
    {
        this.Target = target;
        this.CanKill = canKill;
    }
}

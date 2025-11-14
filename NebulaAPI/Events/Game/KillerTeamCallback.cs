using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Events.Game;

/// <summary>
/// 特定の陣営を除いたキル陣営が生き残っているかどうかの問いに答えます。
/// </summary>
public class KillerTeamCallback : Event
{
    public RoleTeam ExcludedTeam { get; private init; }
    public bool RemainingOtherTeam { get; private set; }
    public void MarkRemaining() => RemainingOtherTeam = true;

    internal KillerTeamCallback(RoleTeam excludedTeam)
    {
        ExcludedTeam = excludedTeam;
        RemainingOtherTeam = false;
    }
}

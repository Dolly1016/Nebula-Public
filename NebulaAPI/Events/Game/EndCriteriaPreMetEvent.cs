using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Game;

public class EndCriteriaPreMetEvent : Event
{
    /// <summary>
    /// 終了条件を満たしたエンディング。
    /// </summary>
    public GameEnd GameEnd { get; private init; }

    /// <summary>
    /// 終了条件を満たした理由。
    /// </summary>
    public GameEndReason EndReason { get; private init; }

    public bool IsBlocked { get; private set; } = false;
    public bool Reject() => IsBlocked = true;
    internal EndCriteriaPreMetEvent(GameEnd end, GameEndReason reason)
    {
        this.GameEnd = end;
        this.EndReason = reason;
    }

}


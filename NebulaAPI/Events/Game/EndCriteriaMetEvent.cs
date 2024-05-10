using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Game;

public class EndCriteriaMetEvent : Event
{
    /// <summary>
    /// 終了条件を満たしたエンディング。
    /// </summary>
    public GameEnd GameEnd { get; private init; }

    /// <summary>
    /// 終了条件を満たした理由。
    /// </summary>
    public GameEndReason EndReason { get; private init; }

    /// <summary>
    /// 乗っ取りの末に実際に至るエンディング。
    /// 乗っ取りが起こらない場合は<see cref="GameEnd"/>と同じものが返ります。
    /// </summary>
    public GameEnd OverwrittenGameEnd { get; private set; }

    /// <summary>
    /// 乗っ取りの末に実際に至るエンディングの終了理由。
    /// 乗っ取りが起こらない場合は<see cref="EndReason"/>と同じものが返ります。
    /// </summary>
    public GameEndReason OverwrittenEndReason { get; private set; }

    public bool TryOverwriteEnd(GameEnd end, GameEndReason reason)
    {
        if(end.Priority >= OverwrittenGameEnd.Priority)
        {
            OverwrittenGameEnd = end;
            OverwrittenEndReason = reason;
            return true;
        }
        return false;
    }

    internal EndCriteriaMetEvent(GameEnd end, GameEndReason reason)
    {
        this.GameEnd = end;
        this.EndReason = reason;
        this.OverwrittenGameEnd = end;
        this.OverwrittenEndReason = reason;
    }
}

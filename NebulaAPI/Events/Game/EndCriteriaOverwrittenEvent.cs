using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Game;

/// <summary>
/// ゲーム終了理由が書き換えられたときに発火します。ゲーム終了理由をさらに書き換えられます。
/// </summary>
/// <remarks>
/// ホストでのみ発火します。
/// </remarks>
public class EndCriteriaOverwrittenEvent : EndCriteriaMetBaseEvent
{
    internal EndCriteriaOverwrittenEvent(GameEnd end, GameEndReason reason, ulong extraWinMask, BitMask<Virial.Game.Player> winners, GameEndDetail detail, WinnerChecker winnerChecker)
        : base(end, reason, extraWinMask, winners, detail, winnerChecker)
    {

    }
}

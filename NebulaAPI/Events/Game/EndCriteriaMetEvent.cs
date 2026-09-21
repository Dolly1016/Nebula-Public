using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;
using Virial.Text;

namespace Virial.Events.Game;

/// <summary>
/// ゲーム終了が決定したときに呼び出されます。
/// ゲーム終了理由を書き換えられます。
/// </summary>
/// <remarks>
/// ホストでのみ発火します。
/// </remarks>
public class EndCriteriaMetEvent : EndCriteriaMetBaseEvent
{
    internal EndCriteriaMetEvent(GameEnd end, GameEndReason reason, int preWinner, WinnerChecker winnerChecker)
        : base(end, reason ,preWinner, winnerChecker)
    {

    }
}

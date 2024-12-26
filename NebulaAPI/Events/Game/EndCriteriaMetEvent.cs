using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Game;

public class EndCriteriaMetEvent : Event
{
    internal delegate (BitMask<Virial.Game.Player> winnersMask, ulong extraWinRawMask) WinnerChecker(int preWinnerMask, Virial.Game.GameEnd end, GameEndReason reason, BitMask<Virial.Game.Player>? lastWinners);
    private WinnerChecker winnerChecker;
    private BitMask<Virial.Game.Player> winnersMask = null!;
    private BitMask<Virial.Game.Player>? lastWinners = null;
    private ulong extraWinRawMask;

    internal ulong ExtraWinRawMask => extraWinRawMask;

    /// <summary>
    /// 現在の勝者
    /// </summary>
    public BitMask<Virial.Game.Player> Winners => winnersMask;

    /// <summary>
    /// 書き換え前の勝者
    /// </summary>
    public BitMask<Virial.Game.Player>? LastWinners => lastWinners;

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
    public bool Overwritten { get; private set; } = false;

    public bool TryOverwriteEnd(GameEnd end, GameEndReason reason, int preWinners = 0)
    {
        if(end.Priority >= OverwrittenGameEnd.Priority)
        {
            OverwrittenGameEnd = end;
            OverwrittenEndReason = reason;
            Overwritten = true;
            CheckWinners(preWinners);
            return true;
        }
        return false;
    }

    internal EndCriteriaMetEvent(GameEnd end, GameEndReason reason, int preWinner, WinnerChecker winnerChecker)
    {
        this.GameEnd = end;
        this.EndReason = reason;
        this.OverwrittenGameEnd = end;
        this.OverwrittenEndReason = reason;
        this.winnerChecker = winnerChecker;

        CheckWinners(preWinner);
    }

    private void CheckWinners(int preWinners = 0)
    {
        lastWinners = winnersMask;
        (winnersMask, extraWinRawMask) = winnerChecker(preWinners, OverwrittenGameEnd, OverwrittenEndReason, lastWinners);
    }
}

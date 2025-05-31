using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Game;

/// <summary>
/// ゲーム終了が決定したときに呼び出されます。
/// ゲーム終了理由を書き換えられます。
/// </summary>
/// <remarks>
/// ホストでのみ発火します。
/// </remarks>
public class EndCriteriaMetEvent : Event
{
    internal delegate (BitMask<Virial.Game.Player> winnersMask, ulong extraWinRawMask) WinnerChecker(int preWinnerMask, Virial.Game.GameEnd end, GameEndReason reason, BitMask<Virial.Game.Player>? lastWinners);
    private WinnerChecker winnerChecker;
    private BitMask<Virial.Game.Player> winnersMask = null!;
    private BitMask<Virial.Game.Player>? lastWinners = null;
    private ulong extraWinRawMask;

    internal ulong ExtraWinRawMask => extraWinRawMask;

    /// <summary>
    /// 現在の勝者。
    /// </summary>
    public BitMask<Virial.Game.Player> Winners => winnersMask;

    /// <summary>
    /// 書き換え前の勝者。
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
    /// <summary>
    /// ゲーム終了理由が上書きされた場合は<c>true</c>を返します。
    /// </summary>
    public bool Overwritten { get; private set; } = false;

    /// <summary>
    /// ゲーム終了理由の上書きを試行します。
    /// ゲーム終了理由の優先度によって上書きできない場合があります。
    /// </summary>
    /// <param name="end">上書きするゲーム終了。</param>
    /// <param name="reason">ゲーム終了理由。</param>
    /// <param name="preWinners">上書き時の勝者マスク。これ以外でもプレイヤーは役職やモディファイア等の都合で勝利できます。</param>
    /// <returns></returns>
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

using Virial.Game;

namespace Virial.Events.Game;

public class EndCriteriaMetBaseEvent : Event
{
    internal delegate (BitMask<Virial.Game.Player> winnersMask, ulong extraWinRawMask, GameEndDetail detail) WinnerChecker(int preWinnerMask, Virial.Game.GameEnd end, GameEndReason reason, BitMask<Virial.Game.Player>? lastWinners);
    private WinnerChecker winnerChecker;
    private BitMask<Virial.Game.Player> originalWinnersMask;
    private BitMask<Virial.Game.Player>? overwrittenWinnersMask = null;
    private ulong originalExtraWinRawMask;
    private ulong? overwrittenExtraWinRawMask = null;
    private GameEnd originalGameEnd;
    private GameEnd? overwrittenGameEnd = null;
    private GameEndReason originalGameEndReason;
    private GameEndReason? overwrittenGameEndReason = null;
    private GameEndDetail originalDetail;
    private GameEndDetail? overwrittenDetail = null;
    private int? originalGameEndCustomPriority = null;
    private int? overwrittenGameEndCustomPriority = null;

    protected private EndCriteriaMetBaseEvent(GameEnd end, GameEndReason reason, int preWinner, WinnerChecker winnerChecker)
    {
        this.originalGameEnd = end;
        this.originalGameEndReason = reason;
        this.winnerChecker = winnerChecker;

        (originalWinnersMask, originalExtraWinRawMask, originalDetail) = this.winnerChecker.Invoke(preWinner, this.originalGameEnd, this.originalGameEndReason, null);
    }

    protected private EndCriteriaMetBaseEvent(GameEnd end, GameEndReason reason, ulong extraWinMask, BitMask<Virial.Game.Player> winners, GameEndDetail detail, WinnerChecker winnerChecker)
    {
        this.originalGameEnd = end;
        this.originalGameEndReason = reason;
        this.originalWinnersMask = winners;
        this.originalDetail = detail;
        this.originalExtraWinRawMask = extraWinMask;
        this.winnerChecker = winnerChecker;
    }

    /// <summary>
    /// 終了条件を満たした乗っ取り前のエンディング。
    /// </summary>
    public GameEnd OriginalGameEnd => originalGameEnd;

    /// <summary>
    /// 終了条件を満たした乗っ取り前の理由。
    /// </summary>
    public GameEndReason OriginalEndReason => originalGameEndReason;

    /// <summary>
    /// 乗っ取り前の追加勝利のマスク。
    /// </summary>
    internal ulong OriginalExtraWinMask => originalExtraWinRawMask;

    /// <summary>
    /// 乗っ取り前の勝者。
    /// </summary>
    public BitMask<Virial.Game.Player> OriginalWinners => originalWinnersMask;

    /// <summary>
    /// 乗っ取り前の勝敗判定詳細。
    /// </summary>
    public GameEndDetail OriginalDetail => originalDetail;

    /// <summary>
    /// 乗っ取りの末に実際に至るエンディング。
    /// 乗っ取りが起こらない場合は<see cref="OriginalGameEnd"/>と同じものが返ります。
    /// </summary>
    internal GameEnd OverwrittenGameEnd => overwrittenGameEnd ?? originalGameEnd;

    /// <summary>
    /// 乗っ取りの末に実際に至るエンディングの終了理由。
    /// 乗っ取りが起こらない場合は<see cref="OriginalEndReason"/>と同じものが返ります。
    /// </summary>
    internal GameEndReason OverwrittenEndReason => overwrittenGameEndReason ?? originalGameEndReason;

    /// <summary>
    /// 乗っ取りの末に実際に発生する追加勝利のマスク。
    /// </summary>
    internal ulong OverwrittenExtraWinMask => overwrittenExtraWinRawMask ?? originalExtraWinRawMask;

    /// <summary>
    /// 乗っ取りの末の勝者。
    /// </summary>
    internal BitMask<Virial.Game.Player> OverwrittenWinners => overwrittenWinnersMask ?? originalWinnersMask;

    /// <summary>
    /// 乗っ取りの末の勝敗判定詳細。
    /// </summary>
    internal GameEndDetail OverwrittenDetail => overwrittenDetail ?? originalDetail;

    internal bool Overwritten => overwrittenGameEnd != null;

    /// <summary>
    /// 乗っ取りを考慮したゲーム終了の優先度。
    /// </summary>
    private int OverwrittenGameEndPriority => overwrittenGameEndCustomPriority ?? overwrittenGameEnd?.Priority ?? originalGameEndCustomPriority ?? originalGameEnd.Priority;
    
    /// <summary>
    /// ゲーム終了理由の上書きを試行します。
    /// ゲーム終了理由の優先度によって上書きできない場合があります。
    /// </summary>
    /// <param name="end">上書きするゲーム終了。</param>
    /// <param name="reason">ゲーム終了理由。</param>
    /// <param name="preWinners">上書き時の勝者マスク。これ以外でもプレイヤーは役職やモディファイア等の都合で勝利できます。</param>
    /// <returns></returns>
    public bool TryOverwriteEnd(GameEnd end, GameEndReason reason, int preWinners = 0) => TryOverwriteEnd(end, end.Priority, reason, preWinners);

    /// <summary>
    /// ゲーム終了理由の上書きを試行します。
    /// ゲーム終了理由の優先度によって上書きできない場合があります。
    /// 本来のゲーム終了と異なる優先度で上書きを試行します。
    /// </summary>
    /// <param name="end">上書きするゲーム終了。</param>
    /// <param name="priority">ゲーム終了の優先度。</param>
    /// <param name="reason">ゲーム終了理由。</param>
    /// <param name="preWinners">上書き時の勝者マスク。これ以外でもプレイヤーは役職やモディファイア等の都合で勝利できます。</param>
    /// <returns></returns>
    public bool TryOverwriteEnd(GameEnd end, int priority, GameEndReason reason, int preWinners = 0)
    {
        if (end == this.OriginalGameEnd) return false;
        if (priority > OverwrittenGameEndPriority)
        {
            overwrittenGameEnd = end;
            overwrittenGameEndCustomPriority = priority;
            overwrittenGameEndReason = reason;
            (overwrittenWinnersMask, overwrittenExtraWinRawMask, overwrittenDetail) = this.winnerChecker.Invoke(preWinners, overwrittenGameEnd, overwrittenGameEndReason.Value, originalWinnersMask);
            return true;
        }
        return false;
    }
}

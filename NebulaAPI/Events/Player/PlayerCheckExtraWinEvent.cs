using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Player;

internal class PlayerCheckExtraWinEvent : AbstractPlayerEvent
{
    public Virial.Game.GameEnd GameEnd { get; private init; }
    public BitMask<Virial.Game.Player> WinnersMask { get; private init; }
    public EditableBitMask<Virial.Game.ExtraWin> ExtraWinMask { get; private init; }

    public ExtraWinCheckPhase Phase { get; private init; }
    public bool IsExtraWin { get; private set; } = false;

    /// <summary>
    /// 追加勝利するかどうかを設定します。
    /// </summary>
    /// <param name="win">追加勝利するかどうか。</param>
    /// <returns>この呼び出しで値が変わったならtrue。</returns>
    public bool SetWin(bool win)
    {
        var lastVal = IsExtraWin;
        IsExtraWin = win;
        return lastVal != IsExtraWin;
    }

    /// <summary>
    /// 条件を満たすなら追加勝利します。
    /// </summary>
    /// <param name="win">追加勝利するかどうか。</param>
    /// <returns>この呼び出しで追加勝利するようになったならtrue。条件を満たさない場合や、既に追加勝利していた場合はfalse。</returns>
    public bool SetWinIf(bool win)
    {
        //条件を満たさないなら何も起きていない。変化なしとして扱う。
        if (!win) return false;

        var lastVal = IsExtraWin;
        IsExtraWin = true;
        return !lastVal;
    }
    public Virial.Game.GameEndDetail Recorder { get; }

    public BitMask<Virial.Game.Player> LastWinners { get; private init; }

    internal PlayerCheckExtraWinEvent(Virial.Game.Player player, BitMask<Virial.Game.Player> winners, EditableBitMask<Virial.Game.ExtraWin> extraWinMask, Virial.Game.GameEnd gameEnd, ExtraWinCheckPhase phase, BitMask<Virial.Game.Player> lastWinners, Virial.Game.GameEndDetail recorder) : base(player)
    {
        this.GameEnd = gameEnd;
        this.WinnersMask = winners;
        this.ExtraWinMask = extraWinMask;
        this.Phase = phase;
        this.LastWinners = lastWinners;
        this.Recorder = recorder;
    }
}

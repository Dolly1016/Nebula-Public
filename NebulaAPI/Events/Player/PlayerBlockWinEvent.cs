using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;


public class PlayerBlockWinEvent : AbstractPlayerEvent
{
    public Virial.Game.GameEnd GameEnd { get; private init; }
    public BitMask<Virial.Game.Player> WinnersMask { get; private init; }
    public bool IsWin { get; private init; }
    public bool IsBlocked { get; private set; } = false;

    /// <summary>
    /// 条件を満たすなら勝利を阻止します。
    /// </summary>
    /// <param name="blocked">阻止するかどうか。</param>
    /// <returns>この呼び出しで阻止されるようになったならtrue。条件を満たさない場合や、既に阻止されていた場合はfalse。</returns>
    public bool SetBlockedIf(bool blocked)
    {
        if (!blocked) return false;

        var lastVal = IsBlocked;
        IsBlocked = true;
        return IsWin && !lastVal;
    }
    public BitMask<Virial.Game.Player> LastWinners { get; private init; }
    public Virial.Game.GameEndDetail Recorder { get; }

    internal PlayerBlockWinEvent(Virial.Game.Player player, BitMask<Virial.Game.Player> winners, Virial.Game.GameEnd gameEnd, BitMask<Virial.Game.Player> lastWinners, Virial.Game.GameEndDetail recorder) : base(player)
    {
        this.GameEnd = gameEnd;
        this.IsWin = winners.Test(player);
        this.WinnersMask = winners;
        this.LastWinners = lastWinners;
        this.Recorder = recorder;
    }
}

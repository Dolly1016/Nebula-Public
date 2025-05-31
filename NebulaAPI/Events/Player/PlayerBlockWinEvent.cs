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
    public bool IsBlocked { get; set; } = false;
    public void SetBlockedIf(bool blocked) => IsBlocked |= blocked;
    public BitMask<Virial.Game.Player> LastWinners { get; private init; }

    internal PlayerBlockWinEvent(Virial.Game.Player player, BitMask<Virial.Game.Player> winners, Virial.Game.GameEnd gameEnd, BitMask<Virial.Game.Player> lastWinners) : base(player)
    {
        this.GameEnd = gameEnd;
        this.IsWin = winners.Test(player);
        this.WinnersMask = winners;
        this.LastWinners = lastWinners;
    }
}

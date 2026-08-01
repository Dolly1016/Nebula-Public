using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Events.Player;

public class PlayerIsSameSideCallback : Event
{
    public Virial.Game.Player Player1 { get; }
    public Virial.Game.Player Player2 { get; }

    private bool markedAsSameSide = false;
    private bool blockedAsDifferentSide = false;
    public bool IsSameSide => !blockedAsDifferentSide && markedAsSameSide;
    public void MarkAsSameSide() => markedAsSameSide = true;
    public void BlockAsDifferentSide() => blockedAsDifferentSide = true;

    internal PlayerIsSameSideCallback(Virial.Game.Player player1, Virial.Game.Player player2)
    {
        this.Player1 = player1;
        this.Player2 = player2;
    }
}


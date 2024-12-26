using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;


public class PlayerCanGuessPlayerLocalEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Guesser { get; private init; }
    public Virial.Game.Player Target => Player;
    public bool CanGuess { get; set; }

    public PlayerCanGuessPlayerLocalEvent(Virial.Game.Player guesser, Virial.Game.Player target, bool canGuess = true) : base(target)
    {
        Guesser = guesser;
        CanGuess = canGuess;
    }
}

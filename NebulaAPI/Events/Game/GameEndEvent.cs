using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// ゲーム終了時に発火します。
/// </summary>
public class GameEndEvent : AbstractGameEvent
{
    /// <summary>
    /// ゲーム終了状態です。
    /// </summary>
    public Virial.Game.EndState EndState { get; private init; }
    internal GameEndEvent(Virial.Game.Game game, Virial.Game.EndState endState) : base(game) {
        this.EndState = endState;
    }
}


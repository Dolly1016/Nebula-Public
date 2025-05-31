using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーがミニゲームコンソールを使用して開くときに呼び出されます。
/// </summary>
/// <remarks>
/// 自身のプレイヤーの操作でのみ発火します。
/// </remarks>
public class PlayerBeginMinigameByConsoleLocalEvent : AbstractPlayerEvent
{
    internal Console Console { get; private init; }
    internal PlayerBeginMinigameByConsoleLocalEvent(Virial.Game.Player player, Console console) : base(player)
    {
        this.Console = console;
    }
}


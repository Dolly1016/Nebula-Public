using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// PlayerRoleSetEventが発火するタイミングとの関係は未定義です。
/// </summary>
public class PlayerRoleSwapEvent : AbstractPlayerEvent
{
    public enum SwapType
    {
        /// <summary>
        /// 役職を交換したケースを表します。
        /// </summary>
        Swap,
        /// <summary>
        /// 役職が複製されたケースを表します。
        /// </summary>
        Duplicate,
    }

    public Virial.Game.Player Source { get; private init; }
    public Virial.Game.Player Destination => this.Player;
    public Virial.Assignable.DefinedRole Role { get; private init; }
    public SwapType Type { get; private init; }
    internal PlayerRoleSwapEvent(Virial.Game.Player source, Virial.Game.Player destination, Assignable.DefinedRole role, SwapType type) : base(destination)
    {
        this.Source = source;
        this.Role = role;
        this.Type = type;
    }
}

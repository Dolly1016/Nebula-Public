using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーが何かを行ったことを知らせるイベントです。
/// </summary>
/// <remarks>
/// 行った本人は <see cref="AbstractPlayerEvent.Player"/> から分かります。
/// </remarks>
public class PlayerDoGameActionEvent : AbstractPlayerEvent
{
    public GameActionType ActionType { get; private init; }
    public Virial.Compat.Vector2 Position { get; private init; }

    /// <summary>
    /// 行動に添えられた数値です。添えられていなければnullです。
    /// </summary>
    /// <remarks>
    /// 何を表すかは行動の種類ごとに決めます。
    /// 設置物のように後から個別に指し示したいものがある場合、その識別子(NebulaSyncObjectのObjectIdなど)を載せます。
    /// </remarks>
    public int? Argument { get; private init; }

    public PlayerDoGameActionEvent(Virial.Game.Player player, GameActionType actionType, Virial.Compat.Vector2 position, int? argument = null): base(player)
    {
        this.ActionType = actionType;
        this.Position = position;
        this.Argument = argument;
    }
}

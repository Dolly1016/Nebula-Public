using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーのようなオブジェクトに関するイベントを表します。
/// </summary>
public class AbstractPlayerlikeEvent : Event
{
    /// <summary>
    /// このイベントに関連するプレイヤーです。
    /// </summary>
    public Virial.Game.IPlayerlike Player { get; private init; }

    internal AbstractPlayerlikeEvent(Virial.Game.IPlayerlike player)
    {
        this.Player = player;
    }
}

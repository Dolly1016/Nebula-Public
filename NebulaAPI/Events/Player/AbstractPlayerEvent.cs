using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーの関するイベントを表します。
/// <see cref="Virial.Game.IBindPlayer"/>を実装する作用素では、<see cref="Virial.Attributes.OnlyMyPlayer"/>属性を付すと自身のプレイヤ―に関するイベントに限って発火させられます。
/// </summary>
public class AbstractPlayerEvent : Event
{
    /// <summary>
    /// このイベントに関連するプレイヤーです。
    /// </summary>
    public Virial.Game.Player Player { get; private init; }

    internal AbstractPlayerEvent(Virial.Game.Player player)
    {
        this.Player = player;
    }
}
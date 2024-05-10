using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

/// <summary>
/// プレイヤーの追放時に呼び出されます。
/// このとき、追加追放者を設定することができます。
/// </summary>
public class PlayerExiledEvent : PlayerDieEvent
{
    internal PlayerExiledEvent(Virial.Game.Player exiled) : base(exiled) { }
}

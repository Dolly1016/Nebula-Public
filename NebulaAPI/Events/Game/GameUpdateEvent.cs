using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

public class GameUpdateEvent : AbstractGameEvent
{
    internal GameUpdateEvent(Virial.Game.Game game) : base(game) { }
}

/// <summary>
/// ここでプレイヤーの輪郭を変更すると、変更が画面に正しく反映されます。
/// </summary>
public class GameHudUpdateEvent : AbstractGameEvent
{
    internal GameHudUpdateEvent(Virial.Game.Game game) : base(game) { }
}
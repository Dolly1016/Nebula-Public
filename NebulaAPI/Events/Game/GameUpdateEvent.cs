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

public class UpdateEvent : Event
{
    internal UpdateEvent() { }
}

/// <summary>
/// ここでプレイヤーの輪郭を変更すると、変更が画面に正しく反映されます。
/// </summary>
public class GameHudUpdateEvent : AbstractGameEvent
{
    internal GameHudUpdateEvent(Virial.Game.Game game) : base(game) { }
}

public class GameHudUpdateFasterEvent : AbstractGameEvent
{
    internal GameHudUpdateFasterEvent(Virial.Game.Game game) : base(game) { }
}

public class GameHudUpdateLaterEvent : AbstractGameEvent
{
    internal GameHudUpdateLaterEvent(Virial.Game.Game game) : base(game) { }
}
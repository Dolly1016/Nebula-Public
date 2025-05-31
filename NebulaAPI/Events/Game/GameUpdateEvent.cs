using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game;

/// <summary>
/// ゲーム中に毎ティック発火します。
/// </summary>
public class GameUpdateEvent : AbstractGameEvent
{
    internal GameUpdateEvent(Virial.Game.Game game) : base(game) { }
}

/// <summary>
/// 毎ティック発火します。
/// </summary>

public class UpdateEvent : Event
{
    internal UpdateEvent() { }
}

/// <summary>
/// ゲームのHUDを更新する際に発火します。
/// ここでプレイヤーの輪郭を変更すると、変更が画面に正しく反映されます。
/// </summary>
public class GameHudUpdateEvent : AbstractGameEvent
{
    internal GameHudUpdateEvent(Virial.Game.Game game) : base(game) { }
}

/// <summary>
/// このイベントの使用は推奨されません。
/// <see cref="GameHudUpdateEvent"/>を使用し、<see cref="Attributes.EventPriority"/>属性でより高い優先度を設定してください。
/// </summary>
[Obsolete("EventPriority属性の使用を検討してください。")]
public class GameHudUpdateFasterEvent : AbstractGameEvent
{
    internal GameHudUpdateFasterEvent(Virial.Game.Game game) : base(game) { }
}

/// <summary>
/// このイベントの使用は推奨されません。
/// <see cref="GameHudUpdateEvent"/>を使用し、<see cref="Attributes.EventPriority"/>属性でより低い優先度を設定してください。
/// </summary>
[Obsolete("EventPriority属性の使用を検討してください。")]
public class GameHudUpdateLaterEvent : AbstractGameEvent
{
    internal GameHudUpdateLaterEvent(Virial.Game.Game game) : base(game) { }
}

/// <summary>
/// LateUpdateのタイミングで発火します。
/// </summary>
public class GameLateUpdateEvent : AbstractGameEvent
{
    internal GameLateUpdateEvent(Virial.Game.Game game) : base(game) { }
}

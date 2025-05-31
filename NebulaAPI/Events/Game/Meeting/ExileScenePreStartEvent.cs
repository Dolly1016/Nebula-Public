using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 追放シーンの開始時に呼び出されます。
/// 会議画面と追放画面の両方にアクセスできます。
/// </summary>
public class ExileSceneStartEvent : Event
{
    IReadOnlyList<Virial.Game.Player> exiled;
    internal ExileSceneStartEvent(IReadOnlyList<Virial.Game.Player> exiled) { this.exiled = exiled ?? []; }
    public IReadOnlyList<Virial.Game.Player> Exiled => exiled;
}

/// <summary>
/// 追放シーンの開始直前に呼び出されます。
/// このとき、追放画面はまだ生成されていません。
/// </summary>
public class ExileScenePreStartEvent : Event
{
    IReadOnlyList<Virial.Game.Player> exiled;
    internal ExileScenePreStartEvent(IReadOnlyList<Virial.Game.Player> exiled) { this.exiled = exiled ?? []; }
    public IReadOnlyList<Virial.Game.Player> Exiled => exiled;
}
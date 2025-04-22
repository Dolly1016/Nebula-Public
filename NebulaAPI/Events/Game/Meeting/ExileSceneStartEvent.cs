using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 追放シーンの開始時に呼び出されます。
/// </summary>
public class ExileSceneStartEvent : Event
{
    IReadOnlyList<Virial.Game.Player> exiled;
    internal ExileSceneStartEvent(IReadOnlyList<Virial.Game.Player> exiled) { this.exiled = exiled ?? []; }
    public IReadOnlyList<Virial.Game.Player> Exiled => exiled;
}
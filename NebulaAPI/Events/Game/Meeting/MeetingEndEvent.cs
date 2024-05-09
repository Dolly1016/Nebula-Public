using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 会議終了時に呼び出されます。
/// プレイヤーはすでに追放されています。
/// </summary>
public class MeetingEndEvent : Event
{
    public IEnumerable<Virial.Game.Player> Exiled { get { foreach(var e in exiled)  yield return e; } }
    private Virial.Game.Player[] exiled;

    internal MeetingEndEvent(Virial.Game.Player[] exiled) {
        this.exiled = exiled;
    }
}
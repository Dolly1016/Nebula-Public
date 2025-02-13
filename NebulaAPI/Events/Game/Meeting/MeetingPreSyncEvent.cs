using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 会議終了時、追加追放者を決定するルーチンの直前で呼び出されます。
/// PlayerExileEventが発火するより前に呼び出されますが、他クライアントのPlayerExileEvent発火によって呼び出されたRPCは呼び出されている可能性があります。
/// </summary>
public class MeetingPreSyncEvent : Event
{
    internal MeetingPreSyncEvent()
    {
    }
}

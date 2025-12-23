using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 会議の開始の要求が受け入れられた時点(レポート/緊急招集の瞬間)で呼び出されます。
/// <see cref="MeetingStartEvent"/>より先行して呼び出されます。
/// </summary>
public class MeetingPreStartEvent : AbstractPlayerEvent
{
    public Virial.Game.Player? Reported { get; private init; }
    public Virial.Game.Player Reporter => Player;
    public Virial.Game.EmergencyMeeting Meeting { get; private init; }
    internal MeetingPreStartEvent(Virial.Game.Player reporter, Virial.Game.Player? reported, Virial.Game.EmergencyMeeting meeting) : base(reporter)
    {
        Reported = reported;
        Meeting = meeting;
    }
}

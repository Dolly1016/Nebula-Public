using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 緊急招集の際に発火するイベントです。
/// </summary>
public class CalledEmergencyMeetingEvent : MeetingPreStartEvent
{
    internal CalledEmergencyMeetingEvent(Virial.Game.Player reporter, Virial.Game.EmergencyMeeting meeting) : base(reporter, null, meeting) { }
}

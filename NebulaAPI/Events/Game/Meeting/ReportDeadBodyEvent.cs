using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 死体が通報されると発火するイベントです。
/// </summary>
public class ReportDeadBodyEvent : MeetingPreStartEvent
{
    internal ReportDeadBodyEvent(Virial.Game.Player reporter, Virial.Game.Player? reported) : base(reporter, reported){ }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 緊急会議開始の要求が受理されたときに発火します。
/// この時点では緊急会議は生成されていません。ホストでのみ発火します。
/// </summary>
internal class MeetingRequestAcceptedHostEvent : AbstractPlayerEvent
{
    public Virial.Game.Player? Reported { get; private init; }
    public Virial.Game.Player Reporter => Player;

    internal MeetingRequestAcceptedHostEvent(Virial.Game.Player reporter, Virial.Game.Player? reported) : base(reporter)
    {
        Reported = reported;
    }
}

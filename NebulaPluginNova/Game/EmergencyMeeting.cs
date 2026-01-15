using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Game;

namespace Nebula.Game;

internal class EmergencyMeetingImpl : EmergencyMeeting
{
    GamePlayer? reported;
    GamePlayer invokedBy;

    internal EmergencyMeetingImpl(GamePlayer? reported, GamePlayer invokedBy)
    {
        this.reported = reported;
        this.invokedBy = invokedBy;
        ModSingleton<EmergencyMeeting>.Instance = this;
    }
    GamePlayer? EmergencyMeeting.ReportedDeadBody => reported;

    GamePlayer EmergencyMeeting.InvokedBy => invokedBy;

    bool ILifespan.IsDeadObject => ModSingleton<EmergencyMeeting>.Instance != this || (!MeetingHud.Instance && !ExileController.Instance);

    void EmergencyMeeting.EditMeetingTime(int deltaSec)
    {
        MeetingHudExtension.RequestEditDiscussionTime.Invoke(deltaSec);
    }

    void EmergencyMeeting.EndVotingForcibly(bool keepCurrentVoting)
    {
        MeetingHudExtension.RequestForceSkip(keepCurrentVoting);
    }
}

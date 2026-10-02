using Nebula.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;
using Virial.Events.Game;

namespace Nebula.Listeners;

internal partial class NebulaGameEventListeners
{
    void SetUpModifiedReportRange(GameStartEvent ev)
    {
        var reportRangeOption = GeneralConfigurations.ReportRangeOption.GetValue();
        if (reportRangeOption == 0) return;
        else if (reportRangeOption == 1)
        {
            var range = GeneralConfigurations.ReportFixedRangeOption;
            PlayerControl.LocalPlayer.MaxReportDistance = range;
        }else if(reportRangeOption == 2)
        {
            GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
            {
                PlayerControl.LocalPlayer.MaxReportDistance = LightPatch.LastCalculatedRange + 0.75f;
            }, NebulaAPI.CurrentGame!);
        }
    }
}
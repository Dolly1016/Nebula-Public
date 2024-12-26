using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class TaskReduction : PerkFunctionalInstance
{
    static PerkFunctionalDefinition def = new("taskReduction", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("taskReduction", 8, 9, new(176, 186, 153)), (def, instance) => new TaskReduction(def, instance));

    private TaskReduction(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        if(MyPlayer.Tasks.IsCrewmateTask && MyPlayer.Tasks.HasExecutableTasks && MyPlayer.Tasks.Quota > 0)
        {
            MyPlayer.Tasks.Unbox().QuotaReduction(1);
        }
    }
}

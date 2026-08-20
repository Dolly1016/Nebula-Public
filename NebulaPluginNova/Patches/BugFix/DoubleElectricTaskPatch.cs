using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.BugFix;

[HarmonyPatch]
internal class DoubleElectricTaskPatch
{
    static MethodBase TargetMethod()
    {
        var method = AccessTools.Method(typeof(SwitchSystem), nameof(SwitchSystem.HasTask));
        return method.MakeGenericMethod(typeof(ElectricTask));
    }

    static bool Prefix(ref bool __result)
    {
        var tasks = PlayerControl.LocalPlayer.myTasks;
        if (tasks.Count > 0)
        {
            var task = tasks[0];
            if (task.TaskType == TaskTypes.FixLights)
            {
                __result = true;
                return false;
            }
        }
        return true;
    }
}

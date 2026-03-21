using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game.Console;

namespace Nebula.Patches;

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.Use))]
internal class SystemConsoleUsePatch
{
    static bool Prefix(SystemConsole __instance)
    {
        return UseButtonAlternative.PreUseConsole(__instance);
    }

    static void Postfix(SystemConsole __instance)
    {
        UseButtonAlternative.PostUseConsole(__instance);
    }
}

[HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.SetOutline))]
internal class SystemConsoleSetOutlinePatch
{
    static bool Prefix(SystemConsole __instance, [HarmonyArgument(0)] bool on, [HarmonyArgument(1)]bool mainTarget)
    {
        return UseButtonAlternative.SetOutline(__instance, on, mainTarget);
    }
}

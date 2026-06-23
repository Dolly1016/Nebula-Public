using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Runtime;

namespace Nebula.Patches;

[HarmonyPatch(typeof(ResolutionManager), nameof(ResolutionManager.SetResolution))]
internal static class ResolutionManagerSetResolutionPatch
{
    public static void Postfix()
    {
        AmongUsLLImpl.Instance.OnScreenSizeChanged();
    }
}
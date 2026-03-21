using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetPipePosition))]
public static class SetPipePositionSuppressExceptionPatch
{
    static Exception Finalizer(Exception __exception)
    {
        return null!;
    }
}
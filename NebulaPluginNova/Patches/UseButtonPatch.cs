using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game.Console;

namespace Nebula.Patches;

[HarmonyPatch(typeof(UseButton), nameof(UseButton.Awake))]
public static class UseButtonAwakePatch
{
    static void Postfix(UseButton __instance)
    {
        UseButtonAlternative.ApplyTo(__instance);
    }
}
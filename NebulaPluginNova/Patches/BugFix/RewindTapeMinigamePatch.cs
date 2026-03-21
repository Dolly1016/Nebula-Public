using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.BugFix;


/// <summary>
/// 高リフレッシュレートで垂直同期がオンのとき、巻き戻し速度の加速が狂う問題。
/// </summary>
[HarmonyPatch(typeof(RewindTapeMinigame), nameof(RewindTapeMinigame.Update))]
internal static class RewindTapeMinigameUpdatePatch
{
    static private bool shouldHelp = false;
    static private float lastDirection = 0f;
    static void Prefix(RewindTapeMinigame __instance)
    {
        shouldHelp = AmongUsUtil.IsInFastUpdate;
        lastDirection = __instance.direction;
    }
    static void Postfix(RewindTapeMinigame __instance)
    {
        if (shouldHelp)
        {
            var absDirection = Mathf.Abs(lastDirection);
            if (0f < absDirection && absDirection < 120f)
            {
                float vanillaDirection = __instance.direction;
                
                if (lastDirection < -1.05f)
                {
                    __instance.direction = lastDirection - 0.1f / AmongUsUtil.FastRate;
                }
                if (lastDirection > 1.05f)
                {
                    __instance.direction = lastDirection + 0.1f / AmongUsUtil.FastRate;
                }

                float backAngle = 5f * (1f - 1f / AmongUsUtil.FastRate);
                __instance.LeftWheel.transform.Rotate(0f, 0f, backAngle);
                __instance.RightWheel.transform.Rotate(0f, 0f, backAngle);
            }
        }
    }
}
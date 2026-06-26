using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.BugFix;

/// <summary>
/// 画面右側のマークが表示されない問題。
/// </summary>
[HarmonyPatch(typeof(DoorCardSwipeGame), nameof(DoorCardSwipeGame.Begin))]
internal static class DoorCardSwipeGameBeginPatch
{
    static void Postfix(DoorCardSwipeGame __instance)
    {
        __instance.confirmSymbol.gameObject.layer = LayerExpansion.GetUILayer();
    }
}

/// <summary>
/// 高リフレッシュレートで垂直同期がオンのとき、カードスワイプがうまくいかない問題。
/// </summary>
[HarmonyPatch(typeof(DoorCardSwipeGame), nameof(DoorCardSwipeGame.Update))]
internal static class DoorCardSwipeGameUpdatePatch
{
    static private bool shouldHelp = false;
    static private float lastY = 0f;
    static void Prefix(DoorCardSwipeGame __instance)
    {
        shouldHelp = AmongUsUtil.IsInFastUpdate && __instance.State == DoorCardSwipeGame.TaskStages.Inserted && Controller.currentTouchType != Controller.TouchType.Joystick;
        lastY = __instance.col.transform.localPosition.y;
    }
    static void Postfix(DoorCardSwipeGame __instance)
    {
        if (shouldHelp)
        {
            var currentY = __instance.col.transform.localPosition.y;
            var diff = lastY - currentY;
            if(diff > 0.01f)
            {
                //ここに入れば元の処理で正しく時間が加算される。
            }
            else if(diff > 0.01f * AmongUsUtil.FastRate)
            {
                __instance.dragTime += Time.deltaTime;
                __instance.confirmSymbol.sprite = null;
                if (!__instance.moving)
                {
                    __instance.moving = true;
                    if (Constants.ShouldPlaySfx())
                    {
                        AmongUsLLImpl.SoundManagerInstance.PlaySound(__instance.CardMove.ToArray().Random(), false, 1f, null);
                    }
                }
            }
        }
    }
}

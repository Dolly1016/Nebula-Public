using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nebula.Online;

namespace Nebula.Patches;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
public class GameStartManagerStartPatch
{
    public static void Postfix(GameStartManager __instance)
    {
        // NoS Online用セットアップ
        MakeOnlineLobby.SetUpButtons(__instance.HostPublicButton, __instance.HostPrivateButton);
    }
}

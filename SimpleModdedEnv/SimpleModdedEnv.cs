using BepInEx.Unity.IL2CPP;
using BepInEx;
using HarmonyLib;

namespace SimpleModdedEnv;

[BepInPlugin("jp.dreamingpig.amongus.nebula.simpleEnv", "SimpleModdedEnv", "1.0.0")]
[BepInProcess("Among Us.exe")]
public class SimpleModdedEnv : BasePlugin
{
    public override void Load()
    {
        new Harmony("jp.dreamingpig.amongus.nebula.simpleEnv").PatchAll();
    }
}

[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
class ServerVersionPatch
{
    static void Postfix(ref int __result)
    {
        __result += 25;
    }
}

[HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
class IsVersionModdedPatch
{
    static bool Prefix(ref bool __result)
    {
        int broadcastVersion = Constants.GetBroadcastVersion();
        __result = Constants.GetVersionComponents(broadcastVersion).Item4 >= 25;
        return false;
    }
}
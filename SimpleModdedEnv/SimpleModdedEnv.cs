using BepInEx.Unity.IL2CPP;
using BepInEx;
using HarmonyLib;

namespace SimpleModdedEnv;

[BepInPlugin("jp.dreamingpig.amongus.nebula.simpleEnv", "SimpleModdedEnv", "1.0.0")]
[BepInProcess("Among Us.exe")]
public class SimpleModdedEnv : BasePlugin
{
    static public BasePlugin MyPlugin;
    public override void Load()
    {
        MyPlugin = this;
        new Harmony("jp.dreamingpig.amongus.nebula.simpleEnv").PatchAll();
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Deserialize))]
class NetworkedPlayerInfoDeserializePatch
{
    static void Prefix(NetworkedPlayerInfo __instance) {
    }
    static void Postfix(NetworkedPlayerInfo __instance)
    {
        SimpleModdedEnv.MyPlugin.Log.LogInfo("Called: NetworkedPlayerInfo");
        SimpleModdedEnv.MyPlugin.Log.LogInfo("RoleWhenAlive Has Value: " + __instance.RoleWhenAlive.HasValue);
    }
}
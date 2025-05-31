using AmongUs.GameOptions;
using InnerNet;
using Nebula.Behavior;
using UnityEngine.ResourceManagement.AsyncOperations;
using Virial;

namespace Nebula.Patches;

public static class AdjustedNumImpostorsModded
{
    public static int GetAdjustedNumImpostorsModded(this AmongUs.GameOptions.IGameOptions options, int players)
    {
        int numImpostors = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
        int num = 3;
        int[] intArray = ModdedOptionValues.MaxImpostors;
        if (intArray != null && players < intArray.Length)
        {
            num = intArray[players];
        }
        return Mathf.Clamp(numImpostors, 0, num);//0を許容する。
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
public static class InitializeRolePatch
{
    static bool Prefix(RoleManager __instance)
    {
        //ロール割り当て
        var players = PlayerControl.AllPlayerControls.GetFastEnumerator().OrderBy(p => Guid.NewGuid()).ToList();
        
        int adjustedNumImpostors = GameOptionsManager.Instance.CurrentGameOptions.GetAdjustedNumImpostorsModded(PlayerControl.AllPlayerControls.Count);

        List<byte> impostors = new();
        List<byte> others = new();

        for (int i = 0; i < players.Count; i++)
            if (i < adjustedNumImpostors)
                impostors.Add(players[i].PlayerId);
            else
                others.Add(players[i].PlayerId);

        NebulaGameManager.Instance!.RoleAllocator = GeneralConfigurations.CurrentGameMode.InstantiateRoleAllocator();
        NebulaGameManager.Instance.RoleAllocator.Assign(impostors, others);
        

        return false;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
public static class StartGamePatch
{
    static void Postfix(GameManager __instance)
    {
        __instance.ShouldCheckForGameEnd = false;
    }
}

[HarmonyPatch(typeof(RoleManager),nameof(RoleManager.AssignRoleOnDeath))]
public static class SetGhostRolePatch
{
    static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
static class CheckTaskCompletionPatch
{
    static bool Prefix(GameManager __instance,ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckEndGameViaTasks))]
static class CheckEndGameViaTasksPatch
{
    static bool Prefix(GameManager __instance, ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
public static class BlockGameOverPatch
{
    public static bool Prefix(LogicGameFlowNormal __instance, ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoCreateOnlineGame))]
static class CreateOnlineGamePatch
{
    static void Prefix(AmongUsClient __instance)
    {
        ConfigurationValues.RestoreAll();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGameHost))]
static class CoStartGameHostPatch
{
    static void Postfix(AmongUsClient __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var original = __result;
        System.Collections.IEnumerator CoStartGameHostMod()
        {
            byte originalMapId = GameOptionsManager.Instance.normalGameHostOptions.MapId;
            //GameOptionsManager.Instance.normalGameHostOptions.MapId = 1;
            //ここで、マップをスポーンさせる前にランダムマップ生成用のパラメータを共有する
            yield return original;
            GameOptionsManager.Instance.normalGameHostOptions.MapId = originalMapId;
        }

        __result = CoStartGameHostMod().WrapToIl2Cpp();
    }
}

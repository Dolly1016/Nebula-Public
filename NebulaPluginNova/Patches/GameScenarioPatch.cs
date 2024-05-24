using Virial;

namespace Nebula.Patches;

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
public static class InitializeRolePatch
{
    static bool Prefix(RoleManager __instance)
    {
        //ロール割り当て
        var players = PlayerControl.AllPlayerControls.GetFastEnumerator().OrderBy(p => Guid.NewGuid()).ToList();
        
        int adjustedNumImpostors = GameOptionsManager.Instance.CurrentGameOptions.GetAdjustedNumImpostors(PlayerControl.AllPlayerControls.Count);

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
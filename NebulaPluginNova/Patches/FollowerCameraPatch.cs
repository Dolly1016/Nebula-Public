namespace Nebula.Patches;

[HarmonyPatch(typeof(FollowerCamera),nameof(FollowerCamera.Update))]
static public class FollowerCameraPatch
{
    public static void Prefix(FollowerCamera __instance)
    {
        try
        {
            var localPlayer = PlayerControl.LocalPlayer;
            if (!__instance.Target) __instance.Target = localPlayer;

            if (localPlayer && localPlayer.lightSource)
            {
                localPlayer.lightSource.transform.SetParent(null);
                localPlayer.lightSource.transform.position = __instance.Target.transform.position;
            }
        }
        catch { }
    }

    public static void Postfix(FollowerCamera __instance)
    {
        NebulaGameManager.Instance?.OnFollowerCameraUpdate();
    }
}

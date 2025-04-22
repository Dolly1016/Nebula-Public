namespace Nebula.Patches;

[HarmonyPatch(typeof(FollowerCamera),nameof(FollowerCamera.Update))]
static public class FollowerCameraPatch
{
    public static void Prefix(FollowerCamera __instance)
    {
        try
        {
            if (!__instance.Target) __instance.Target = PlayerControl.LocalPlayer;

            if (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.lightSource)
            {
                PlayerControl.LocalPlayer.lightSource.transform.SetParent(null);
                PlayerControl.LocalPlayer.lightSource.transform.position = __instance.Target.transform.position;
            }
        }
        catch { }
    }

    public static void Postfix(FollowerCamera __instance)
    {
        NebulaGameManager.Instance?.OnFollowerCameraUpdate();
    }
}

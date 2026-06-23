namespace Nebula.Patches;

[HarmonyPatch(typeof(FollowerCamera),nameof(FollowerCamera.Update))]
static public class FollowerCameraPatch
{
    public static void Prefix(FollowerCamera __instance)
    {
        NebulaProfiler.LapTimer("Before FollowerCamera.Update");
        try
        {
            MonoBehaviour target = __instance.Target;
            var localPlayer = AmongUsLLImpl.LocalPlayer;
            if (!target.AsBoolFast())
            {
                __instance.Target = localPlayer;
                target = localPlayer;
            }

            if (localPlayer.AsBoolFast())
            {
                var lightSource = localPlayer.lightSource;
                if (lightSource.AsBoolFast())
                {
                    var lightSourceTransform = lightSource.transform;
                    lightSourceTransform.SetParent(null);
                    lightSourceTransform.position = target.transform.position;
                }
            }
        }
        catch { }
        NebulaProfiler.LapTimer("FollowerCamera.Update");
    }

    public static void Postfix(FollowerCamera __instance)
    {
        NebulaGameManager.Instance?.OnFollowerCameraUpdate();
    }
}

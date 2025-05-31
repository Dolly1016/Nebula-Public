using Nebula.Behavior;

namespace Nebula.Patches;

//Camera (Skeld)
[HarmonyPatch(typeof(SurveillanceMinigame), nameof(SurveillanceMinigame.Begin))]
class SurveillanceMinigameBeginPatch
{
    public static void Prefix(SurveillanceMinigame __instance)
    {
        if(!__instance.CameraPrefab.gameObject.TryGetComponent<IgnoreShadowCamera>(out _))
        {
            var isc = __instance.CameraPrefab.gameObject.AddComponent<IgnoreShadowCamera>();
            isc.ShowNameText = false;
        }

        NebulaGameManager.Instance?.ConsoleRestriction?.ShowTimerIfNecessary(ConsoleRestriction.ConsoleType.Camera, __instance.transform, new Vector3(3.4f, 2f, -50f));
    }

    public static void Postfix(SurveillanceMinigame __instance)
    {
        __instance.gameObject.GetComponentsInChildren<IgnoreShadowCamera>().Do(isc => isc.ShowNameText = false);
    }
}

[HarmonyPatch(typeof(SurveillanceMinigame), nameof(SurveillanceMinigame.Update))]
class SurveillanceMinigameUpdatePatch
{
    public static bool Prefix(SurveillanceMinigame __instance)
    {
        if (ConsoleTimer.IsOpenedByAvailableWay()) return true;

        for (int j = 0; j < __instance.ViewPorts.Length; j++)
        {
            __instance.ViewPorts[j].sharedMaterial = __instance.StaticMaterial;
            __instance.SabText[j].gameObject.SetActive(true);
            __instance.SabText[j].text = Language.Translate("console.notAvailable");
        }
        return false;
    }
}

//Camera (Fungle)
[HarmonyPatch(typeof(FungleSurveillanceMinigame), nameof(FungleSurveillanceMinigame.Begin))]
class FungleSurveillanceMinigameBeginPatch
{
    public static void Prefix(FungleSurveillanceMinigame __instance)
    {
        var isc = __instance.securityCamera.cam.gameObject.AddComponent<IgnoreShadowCamera>();
        isc.ShowNameText = false;
    }
}

//Camera (Others)
[HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.Begin))]
class PlanetSurveillanceMinigameBeginPatch
{
    public static void Prefix(PlanetSurveillanceMinigame __instance)
    {
        var isc = __instance.Camera.gameObject.AddComponent<IgnoreShadowCamera>();
        isc.ShowNameText = false;

        NebulaGameManager.Instance?.ConsoleRestriction?.ShowTimerIfNecessary(ConsoleRestriction.ConsoleType.Camera, __instance.transform, new Vector3(0f, -1.9f, -50f));
    }
}

[HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.Update))]
class PlanetSurveillanceMinigameUpdatePatch
{
    public static bool Prefix(PlanetSurveillanceMinigame __instance)
    {
        if (ConsoleTimer.IsOpenedByAvailableWay()) return true;

        __instance.SabText.gameObject.SetActive(true);
        __instance.SabText.text = Language.Translate("console.notAvailable");
        __instance.isStatic = true;
        __instance.ViewPort.sharedMaterial = __instance.StaticMaterial;

        return false;
    }
}

[HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.NextCamera))]
class PlanetSurveillanceMinigameNextCameraPatch
{
    public static bool Prefix(PlanetSurveillanceMinigame __instance, [HarmonyArgument(0)]int direction)
    {
        if (ConsoleTimer.IsOpenedByAvailableWay()) return true;

        if (direction != 0 && Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(__instance.ChangeSound, false, 1f, null);
        
        __instance.Dots[__instance.currentCamera].sprite = __instance.DotDisabled;
        __instance.currentCamera = (__instance.currentCamera + direction).Wrap(__instance.survCameras.Length);
        __instance.Dots[__instance.currentCamera].sprite = __instance.DotEnabled;
        SurvCamera survCamera = __instance.survCameras[__instance.currentCamera];
        __instance.Camera.transform.position = survCamera.transform.position + __instance.survCameras[__instance.currentCamera].Offset;
        __instance.LocationName.text = ((survCamera.NewName > StringNames.None) ? DestroyableSingleton<TranslationController>.Instance.GetString(survCamera.NewName) : survCamera.CamName);
        
        return false;
    }
}

//Door Log
//Camera (Others)
[HarmonyPatch(typeof(SecurityLogGame), nameof(SecurityLogGame.Awake))]
class SecurityLogGameBeginPatch
{
    public static void Prefix(SecurityLogGame __instance)
    {
        NebulaGameManager.Instance?.ConsoleRestriction?.ShowTimerIfNecessary(ConsoleRestriction.ConsoleType.Camera, __instance.transform, new Vector3(3.4f, 1.5f, -50f));
    }
}

[HarmonyPatch(typeof(SecurityLogGame), nameof(SecurityLogGame.Update))]
class SecurityLogGameUpdatePatch
{
    public static bool Prefix(SecurityLogGame __instance)
    {
        if (ConsoleTimer.IsOpenedByAvailableWay()) return true;

        __instance.SabText.gameObject.SetActive(true);
        __instance.SabText.text = Language.Translate("console.notAvailable");
        __instance.EntryPool.ReclaimAll();

        return false;
    }
}
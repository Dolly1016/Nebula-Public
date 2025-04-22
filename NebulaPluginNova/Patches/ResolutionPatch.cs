using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Runtime;

namespace Nebula.Patches;

[NebulaPreprocess(PreprocessPhase.BuildNoSModuleContainer)]
internal static class ResolutionSetUp
{
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        //解像度設定
        var currentResolution = Screen.fullScreen ? Screen.currentResolution : new Resolution() { width = Screen.width, height = Screen.height };
        if (currentResolution.width * 9 != currentResolution.height * 16)
        {
            var cand = Screen.resolutions.Where(r => r.width * 9 == r.height * 16).ToArray();
            if (cand.Length > 0)
            {
                var selected = cand.MaxBy(r => r.width);
                ResolutionManager.SetResolution(selected.width, selected.height, Screen.fullScreen);
            }
        }
    }
}

[HarmonyPatch(typeof(ResolutionSlider), nameof(ResolutionSlider.OnEnable))]
internal static class ResolutionSettingPatch
{
    public static void Postfix(ResolutionSlider __instance)
    {
        var cand = __instance.allResolutions.Where(r => r.width * 9 == r.height * 16).ToArray();
        if (cand.Length > 0)
        {
            __instance.allResolutions = cand;
            __instance.targetIdx = cand.FindIndex(r => r.width == __instance.targetResolution.width && r.height == __instance.targetResolution.height);
            __instance.slider.Value = (float)__instance.targetIdx / ((float)__instance.allResolutions.Length - 1f);
        }
    }
}

[HarmonyPatch(typeof(ResolutionSlider), nameof(ResolutionSlider.OnResChange))]
internal static class UnrecommendedResolutionSettingPatch
{
    public static void Postfix(ResolutionSlider __instance)
    {
        if(__instance.targetResolution.width * 9 != __instance.targetResolution.height * 16)
        {
            DebugScreen.Push(new FunctionalDebugTextContent(() => Language.Translate("ui.warning.unrecommendedResolution"), Virial.FunctionalLifespan.GetTimeLifespan(5f)));
        }
    }
}

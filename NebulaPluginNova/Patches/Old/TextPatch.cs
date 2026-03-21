// TextTranslatorTMPを通してMODテキストの翻訳をさせるためのパッチ

namespace Nebula.Patches;

[HarmonyPatch(typeof(TextTranslatorTMP),nameof(TextTranslatorTMP.ResetText))]
public static class TextPatch
{
    static public bool Prefix(TextTranslatorTMP __instance)
    {
        if ((short)__instance.TargetText != short.MaxValue) return true;
        __instance.GetComponent<TMPro.TextMeshPro>().text = Language.Translate(__instance.defaultStr);
        return false;
    }
}

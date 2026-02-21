namespace Nebula.Patches;

[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
public static class VersionShowerPatch
{
    static void Postfix(VersionShower __instance)
    {
        string text = __instance.text.text;
        int last = text.IndexOf('(');
        if(last != -1)text = text.Substring(0, last);
        __instance.text.text = NebulaPlugin.GetNebulaVersionString() + " on AU " + text;
        __instance.text.ForceMeshUpdate();

        var buttonObj = UnityHelper.CreateObject("CopyButton", __instance.text.transform, Vector3.zero);
        buttonObj.SetUpButton().OnClick.AddListener(()=> {
            ClipboardHelper.PutClipboardString(__instance.text.text);
            DebugScreen.Push(Language.Translate("ui.version.copied"), 3f);
        });
        var buttonCollider = buttonObj.AddComponent<BoxCollider2D>();
        buttonCollider.size = __instance.text.rectTransform.sizeDelta;
        buttonCollider.isTrigger = true;


        ModManager.Instance.ShowModStamp();
    }
}
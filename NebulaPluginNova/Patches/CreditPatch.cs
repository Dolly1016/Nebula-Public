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


        ModManager.Instance.ShowModStamp();
    }
}
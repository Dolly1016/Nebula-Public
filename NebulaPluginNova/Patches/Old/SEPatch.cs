using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches;

[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlaySound))]
public static class SoundPatch
{
    public static bool Prefix(SoundManager __instance, [HarmonyArgument(0)] AudioClip clip)
    {
        //シーカー変身音声は流さない(Berserkerの方向は距離減衰版の別経路で流している)　そのほか、使用箇所が無いためここで全部ブロック
        if (clip.name == "HnS_ImpostorScream") return false;
        
        return true;
    }
}


[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.UpdateChannelVolumes))]
public static class OverwriteChannelVolumePatch
{
    public static void Postfix(SoundManager __instance)
    {
        ClientOption.TryChangeAmbientVolumeImmediately();
    }
}

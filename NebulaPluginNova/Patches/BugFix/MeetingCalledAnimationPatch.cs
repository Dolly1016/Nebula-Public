using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;

namespace Nebula.Patches.BugFix;

[HarmonyPatch(typeof(MeetingCalledAnimation), nameof(MeetingCalledAnimation.Initialize))]
internal static class MeetingCalledAnimationInitializePatch
{
    static void Postfix(MeetingCalledAnimation __instance)
    {
        GameObject.Destroy(__instance.GetComponent<SortingGroup>());

        if (__instance.playerParts.TryGetComponent<NebulaCosmeticsLayer>(out var layer)) layer.SetSortingProperty(true, 1f, 0);
        if (__instance.classicPlayerParts.TryGetComponent<NebulaCosmeticsLayer>(out var classicLayer)) classicLayer.SetSortingProperty(true, 1f, 0);
        __instance.emergencyClassicParent.transform.GetChild(1).GetChild(1).gameObject.SetActive(false);
    }
}

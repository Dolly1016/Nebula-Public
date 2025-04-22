using AmongUs.GameOptions;
using AmongUs.HTTP;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using static HttpMatchmakerManager;
using static Il2CppSystem.Globalization.CultureInfo;

namespace Nebula.Patches;

[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Start))]
public static class CreateGameOptionsPatch
{
    static void Postfix(CreateGameOptions __instance)
    {
        __instance.tooltip.transform.parent.gameObject.SetActive(false);
        __instance.SelectMode(0, true);
        __instance.modeButtons[0].transform.parent.gameObject.SetActive(false);
        __instance.mapPicker.transform.SetLocalY(-1.245f);
        __instance.capacityOption.transform.SetLocalY(-1.15f);
        __instance.levelButtons[0].transform.parent.gameObject.SetActive(false);
        __instance.serverButton.transform.parent.SetLocalY(-1.84f);
        __instance.serverDropdown.transform.SetLocalY(-2.63f);
    }
}

[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.UpdateServerText))]
public static class CreateGameOptionsUpdateRegionPatch
{
    static void Postfix(CreateGameOptions __instance)
    {
        __instance.capacityOption.ValidRange.max = AmongUsUtil.IsCustomServer() ? 24 : 15;

        __instance.capacityOption.Value = __instance.capacityOption.ValidRange.Clamp(__instance.capacityOption.Value);
        __instance.capacityOption.UpdateValue();
        __instance.capacityOption.AdjustButtonsActiveState();
    }
}


[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Confirm))]
public static class CreateGameOptionsConfirmPatch
{
    static bool Prefix(CreateGameOptions __instance)
    {
        if (!DestroyableSingleton<MatchMaker>.Instance.Connecting<CreateGameOptions>(__instance)) return false;
        
        GameOptionsManager.Instance.GameHostOptions.TryGetInt(Int32OptionNames.MaxPlayers, out var num);
        int[] array = ModdedOptionValues.MaxImpostors;
        GameOptionsManager.Instance.GameHostOptions.TryGetInt(Int32OptionNames.NumImpostors, out var num2);
        if (num2 > array[num])
            GameOptionsManager.Instance.GameHostOptions.SetInt(Int32OptionNames.NumImpostors, array[num]);
        if (num2 == 0)
            GameOptionsManager.Instance.GameHostOptions.SetInt(Int32OptionNames.NumImpostors, 1);
        __instance.CoStartGame();

        return false;
    }
}

/*
[HarmonyPatch(typeof(NormalGameOptionsV09), nameof(NormalGameOptionsV09.TryGetIntArray))]
public static class TryGetIntArrayV09Patch
{
    static bool Prefix(NormalGameOptionsV09 __instance, ref bool __result, [HarmonyArgument(0)]Int32ArrayOptionNames optionName, [HarmonyArgument(1)]ref Il2CppStructArray<int> value)
    {
        if (optionName == Int32ArrayOptionNames.MaxImpostors)
        {
            value = ModdedOptionValues.MaxImpostors;
            __result = true;
            return false;
        }
        if (optionName == Int32ArrayOptionNames.MinPlayers)
        {
            value = ModdedOptionValues.MinPlayers;
            __result = true;
            return false;
        }
        value = null!;
        __result = false;
        return false;
    }
}
*/

//TryGetIntArrayにパッチが当てられない？のでしばらくの間の応急措置
[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.SetUpFromData))]
public static class TryGetIntArrayV09Patch
{
    static bool Prefix(NumberOption __instance, [HarmonyArgument(0)] BaseGameSetting data, [HarmonyArgument(1)] int maskLayer)
    {
        if (data.Type == OptionTypes.Int && data.Title == StringNames.GameNumImpostors)
        {
            __instance.data = data;
            __instance.GetComponentsInChildren<SpriteRenderer>(true).Do(r => r.material.SetInt(PlayerMaterial.MaskLayer, maskLayer));
            __instance.GetComponentsInChildren<TextMeshPro>(true).Do(t =>
            {
                t.fontMaterial.SetFloat("_StencilComp", 3f);
                t.fontMaterial.SetFloat("_Stencil", (float)maskLayer);
            });

            IntGameSetting intGameSetting = data.Cast<IntGameSetting>();

            __instance.Title = intGameSetting.Title;
            __instance.Value = (float)intGameSetting.Value;
            __instance.Increment = (float)intGameSetting.Increment;
            int num;
            GameOptionsManager.Instance.CurrentGameOptions.TryGetInt(Int32OptionNames.MaxPlayers, out num);
            int[] array = ModdedOptionValues.MaxImpostors;
            __instance.ValidRange = new FloatRange((float)intGameSetting.ValidRange.min, (float)array[num]);
            __instance.FormatString = intGameSetting.FormatString;
            __instance.ZeroIsInfinity = intGameSetting.ZeroIsInfinity;
            __instance.SuffixType = intGameSetting.SuffixType;
            __instance.intOptionName = intGameSetting.OptionName;
            return false;
        }
        return true;
    }
}

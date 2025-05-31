using Nebula.Behavior;
using UnityEngine.UIElements;
using Virial.Game;

namespace Nebula.Patches;

//Vitals
[HarmonyPatch(typeof(VitalsMinigame), nameof(VitalsMinigame.Begin))]
class VitalsMinigameBeginPatch
{
    static private float[] PanelAreaScale = [1f, 0.95f, 0.76f];
    static private (int x, int y)[] PanelAreaSize = [(3, 5), (3, 6), (4, 6)];
    static private Vector3[] PanelAreaOffset = [new(0f, 0f, -1f), new(0.1f, 0.145f, -1f), new(-0.555f, 0f, -1f)];
    static private (float x, float y)[] PanelAreaMultiplier = [(1f, 1f), (1f, 0.89f), (0.974f, 1f)];
    //static private int GetVotingAreaType(int players) => players <= 15 ? 0 : players <= 18 ? 1 : 2;
    private static Vector3 ToVoteAreaPos(VitalsMinigame minigame, int index, int arrangeType) => 
        More15Helpers.ConvertPos(index, arrangeType, PanelAreaSize, new(minigame.XStart, minigame.YStart, -1f), PanelAreaOffset, new(minigame.XOffset, minigame.YOffset), PanelAreaScale, PanelAreaMultiplier);

    static void Postfix(VitalsMinigame __instance)
    {
        NebulaGameManager.Instance?.ConsoleRestriction?.ShowTimerIfNecessary(ConsoleRestriction.ConsoleType.Vitals, __instance.transform, new Vector3(3.4f, 2f, -50f));
        __instance.BatteryText.gameObject.SetActive(false);
        VitalsMinigameUpdatePatch.UpdateVitals(__instance,true);

        //バイタルパネルではスキンを透明にしない
        int n = 0;
        int displayType = More15Helpers.GetDisplayType(__instance.vitals.Count);
        foreach (var panel in __instance.vitals)
        {
            panel.PlayerIcon.cosmetics.SetSkin(panel.PlayerInfo.DefaultOutfit.SkinId, panel.PlayerInfo.DefaultOutfit.ColorId);
            panel.PlayerIcon.cosmetics.SetHatColor(Palette.White);
            panel.PlayerIcon.cosmetics.SetVisorAlpha(Palette.White.a);

            panel.transform.localPosition = ToVoteAreaPos(__instance, n, displayType);
            panel.transform.localScale *= PanelAreaScale[displayType];
            n++;
        }
    }
}

[HarmonyPatch(typeof(VitalsMinigame), nameof(VitalsMinigame.Update))]
class VitalsMinigameUpdatePatch
{
    public static void UpdateVitals(VitalsMinigame __instance,bool forcely = false)
    {
        if (__instance.SabText.isActiveAndEnabled && !PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer))
        {
            __instance.SabText.gameObject.SetActive(false);
            foreach (var v in __instance.vitals)v.gameObject.SetActive(true);
        }
        else if (!__instance.SabText.isActiveAndEnabled && PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer))
        {
            __instance.SabText.gameObject.SetActive(true);
            foreach (var v in __instance.vitals) v.gameObject.SetActive(false);
        }

        var vitals = FakeInformation.Instance!.CurrentVitals;

        foreach (var v in __instance.vitals)
        {
            var myInfo = vitals.Players.FirstOrDefault(p => p.playerId == v.PlayerInfo.PlayerId);

            var modInfo = NebulaGameManager.Instance?.GetPlayer(v.PlayerInfo.PlayerId);

            if (myInfo.state == VitalsState.Disconnected || (modInfo?.HasAttribute(PlayerAttributes.Isolation) ?? false))
            {
                if (!v.IsDiscon || forcely)
                {
                    v.SetDisconnected();
                }
            }
            else if (myInfo.state == VitalsState.Dead && !(modInfo?.HasAttribute(PlayerAttributes.BuskerEffect) ?? false))
            {
                if (!v.IsDead || forcely)
                {
                    v.SetDead();
                    v.Cardio.gameObject.SetActive(true);
                }
            }
            else
            {
                if (v.IsDiscon || v.IsDead || forcely)
                {
                    v.IsDiscon = false;
                    v.IsDead = false;
                    v.SetAlive();
                    v.Background.sprite = __instance.PanelPrefab.Background.sprite;
                    v.Cardio.gameObject.SetActive(true);
                }
            }
        }

        
    }


    static bool Prefix(VitalsMinigame __instance)
    {
        if (ConsoleTimer.IsOpenedByAvailableWay())
        {
            UpdateVitals(__instance);
            return false;
        }

        __instance.SabText.gameObject.SetActive(true);
        __instance.SabText.text = Language.Translate("console.notAvailable");

        foreach (var panel in __instance.vitals) panel.gameObject.SetActive(false);

        return false;
    }
}


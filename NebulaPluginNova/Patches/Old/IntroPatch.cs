
using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Patches;


[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
public static class ShowIntroPatch
{
    static void OnDestroy()
    {
        NebulaGameManager.Instance?.OnGameStart();
        HudManager.Instance.ShowVanillaKeyGuide();
    }

    static bool Prefix(IntroCutscene __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        //ゲームモードに沿ってモジュールを追加
        NebulaAPI.CurrentGame!.SetGameMode(GeneralConfigurations.CurrentGameMode.InstantiateModule());

        __result = CoBegin(__instance).WrapToIl2Cpp();
        return false;
    }

    static IEnumerator CoBegin(IntroCutscene __instance)
    {
        IntroCutscene.Instance = __instance;

        HudManager.Instance.HideGameLoader();

        SoundManager.Instance.PlaySound(__instance.IntroStinger, false, 1f, null);

        __instance.HideAndSeekPanels.SetActive(false);
        __instance.CrewmateRules.SetActive(false);
        __instance.ImpostorRules.SetActive(false);
        __instance.ImpostorName.gameObject.SetActive(false);
        __instance.ImpostorTitle.gameObject.SetActive(false);
        __instance.ImpostorText.gameObject.SetActive(false);


        IEnumerable<PlayerControl> shownPlayers = PlayerControl.AllPlayerControls.GetFastEnumerator().OrderBy(p => p.AmOwner ? 0 : 1);
        var myInfo = GamePlayer.LocalPlayer;

        var introInfo = GameOperatorManager.Instance!.Run(new GameShowIntroLocalEvent(myInfo!.Role.Role.Team, myInfo!.Role));

        switch (introInfo.RevealType)
        {
            case Virial.Assignable.TeamRevealType.OnlyMe:
                shownPlayers = new PlayerControl[] { PlayerControl.LocalPlayer };
                break;
            case Virial.Assignable.TeamRevealType.Teams:
                shownPlayers = shownPlayers.Where(p => p.GetModInfo()?.Role.Role.Team == myInfo.Role.Role.Team);
                break;
        }

        if(GeneralConfigurations.MapFlipXOption || GeneralConfigurations.MapFlipYOption)
        {
            Vector2 vec = new(1f, 1f);
            if (GeneralConfigurations.MapFlipXOption) {
                PlayerModInfo.RpcAttrModulator.LocalInvoke((myInfo!.PlayerId, new AttributeModulator(PlayerAttributes.FlipX, 100000f, true, 0, null, false), true));
                vec.x = -1f;
            }
            if (GeneralConfigurations.MapFlipYOption)
            {
                PlayerModInfo.RpcAttrModulator.LocalInvoke((myInfo!.PlayerId, new AttributeModulator(PlayerAttributes.FlipY, 100000f, true, 0, null, false), true));
                vec.y = -1f;
            }
            PlayerModInfo.RpcAttrModulator.LocalInvoke((myInfo!.PlayerId, new SpeedModulator(1f, vec, true, 100000f, true, 0, null, false), true));
        }

        yield return CoShowTeam(__instance,myInfo!,shownPlayers.ToArray(), 3f, introInfo);
        yield return CoShowRole(__instance,myInfo!, introInfo);
        ShipStatus.Instance.StartSFX();
        OnDestroy();
        GameObject.Destroy(__instance.gameObject);
    }

    static IEnumerator CoShowTeam(IntroCutscene __instance, GamePlayer myInfo, PlayerControl[] shownPlayers, float duration, GameShowIntroLocalEvent introInfo)
    {
#if PC
        if (__instance.overlayHandle == null)
        {
            __instance.overlayHandle = DestroyableSingleton<DualshockLightManager>.Instance.AllocateLight();
        }
#endif
        yield return ShipStatus.Instance.CosmeticsCache.PopulateFromPlayers();

        Color fromC = introInfo.TeamColor.ToUnityColor();
        Color toC = introInfo.TeamFadeColor?.ToUnityColor() ?? fromC;

        Vector3 position = __instance.BackgroundBar.transform.position;
        position.y -= 0.25f;
        __instance.BackgroundBar.transform.position = position;
        __instance.BackgroundBar.material.SetColor("_Color", fromC);
        __instance.TeamTitle.text = introInfo.TeamName;
        __instance.TeamTitle.color = fromC;
        int maxDepth = Mathn.CeilToInt(7.5f);
        for (int i = 0; i < shownPlayers.Length; i++)
        {
            PlayerControl playerControl = shownPlayers[i];
            if (playerControl)
            {
                NetworkedPlayerInfo data = playerControl.Data;
                if (data != null)
                {
                    PoolablePlayer poolablePlayer = __instance.CreatePlayer(i, maxDepth, data, false);
                    if (i == 0 && data.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                    {
                        __instance.ourCrewmate = poolablePlayer;
                    }
                }
            }
        }

#if PC
        __instance.overlayHandle.color = fromC;
#endif
        
        Color fade = Color.black;
        Color impColor = Color.white;
        Vector3 titlePos = __instance.TeamTitle.transform.localPosition;
        float timer = 0f;

        float madFadeBegin = 1.8f, madFadeEnd = 2.4f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float num = Mathn.Min(1f, timer / duration);
            __instance.Foreground.material.SetFloat("_Rad", __instance.ForegroundRadius.ExpOutLerp(num * 2f));
            fade.a = Mathn.Lerp(1f, 0f, num * 3f);
            __instance.FrontMost.color = fade;


            float p = timer < madFadeBegin ? 0f : timer > madFadeEnd ? 1f : (timer - madFadeBegin) / (madFadeEnd - madFadeBegin);
            __instance.BackgroundBar.material.SetColor("_Color", Color.Lerp(fromC, toC, p));

            Color c = fromC;
            c.a = Mathn.Clamp(FloatRange.ExpOutLerp(num, 0f, 1f), 0f, 1f);
            __instance.TeamTitle.color = c;
            __instance.RoleText.color = c;
            impColor.a = Mathn.Lerp(0f, 1f, (num - 0.3f) * 3f);
            __instance.ImpostorText.color = impColor;
            titlePos.y = 2.7f - num * 0.3f;
            __instance.TeamTitle.transform.localPosition = titlePos;
#if PC
            __instance.overlayHandle.color = c.AlphaMultiplied(Mathn.Min(1f, timer * 2f));
#endif
            yield return null;
        }
        timer = 0f;
        while (timer < 1f)
        {
            timer += Time.deltaTime;
            float num2 = timer / 1f;
            fade.a = Mathn.Lerp(0f, 1f, num2 * 3f);
            __instance.FrontMost.color = fade;
#if PC
            __instance.overlayHandle.color = fromC.AlphaMultiplied(1f - fade.a);
#endif
            yield return null;
        }
        yield break;
    }

    static IEnumerator CoShowRole(IntroCutscene __instance, GamePlayer myInfo, GameShowIntroLocalEvent introInfo)
    {
        var role = myInfo.Role;
        __instance.RoleText.text = introInfo.RoleName;
        __instance.RoleBlurbText.text = introInfo.RoleBlurb;
        __instance.RoleBlurbText.transform.localPosition = new(0.0965f, -2.12f, -36f);
        __instance.RoleBlurbText.rectTransform.sizeDelta = new(12.8673f, 0.7f);
        __instance.RoleBlurbText.alignment = TMPro.TextAlignmentOptions.Top;

        foreach(var m in myInfo.Modifiers)
        {
            string? mBlurb = m.DisplayIntroBlurb;
            if (mBlurb != null) __instance.RoleBlurbText.text += "\n" + mBlurb;
        }

        var unityColor = introInfo.RoleColor.ToUnityColor();
        __instance.RoleText.color = unityColor;
        __instance.YouAreText.color = unityColor;
        __instance.RoleBlurbText.color = unityColor;
        SoundManager.Instance.PlaySound(PlayerControl.LocalPlayer.Data.Role.IntroSound, false, 1f, null);
        __instance.YouAreText.gameObject.SetActive(true);
        __instance.RoleText.gameObject.SetActive(true);
        __instance.RoleBlurbText.gameObject.SetActive(true);
        if (__instance.ourCrewmate == null)
        {
            __instance.ourCrewmate = __instance.CreatePlayer(0, 1, PlayerControl.LocalPlayer.Data, false);
            __instance.ourCrewmate.gameObject.SetActive(false);
        }
        __instance.ourCrewmate.gameObject.SetActive(true);
        __instance.ourCrewmate.transform.localPosition = new Vector3(0f, -1.05f, -18f);
        __instance.ourCrewmate.transform.localScale = new Vector3(1f, 1f, 1f);
        __instance.ourCrewmate.ToggleName(false);
        yield return new WaitForSeconds(2.5f);
        __instance.YouAreText.gameObject.SetActive(false);
        __instance.RoleText.gameObject.SetActive(false);
        __instance.RoleBlurbText.gameObject.SetActive(false);
        __instance.ourCrewmate.gameObject.SetActive(false);
        yield break;
    }
}


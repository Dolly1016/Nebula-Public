using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using Nebula.Behaviour;
using UnityEngine;
using Virial.Game;
using Nebula.Modules;
using Il2CppSystem.Collections;
using UnityEngine.SceneManagement;
using Virial.Assignable;
using Nebula.Roles;
using Virial.Events.Game;
using TMPro;
using Nebula.Modules.GUIWidget;
using UnityEngine.Rendering;

namespace Nebula.Patches;


[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
public class GameStartManagerUpdatePatch
{
    public static bool LastChecked = false;
    public static float ShareOnLobbyTimer = 10f;
    public static bool Prefix(GameStartManager __instance)
    {
        if (!GameData.Instance) return false;
        if (!GameManager.Instance) return false;

        //公開ルームではスライド使用不可 (不特定多数への画像配信を禁止)
        if (AmongUsClient.Instance.IsGamePublic) NebulaGameManager.Instance?.LobbySlideManager.Abandon();

        __instance.MinPlayers = GeneralConfigurations.CurrentGameMode.MinPlayers;

        try
        {
            __instance.UpdateMapImage((MapNames)GameManager.Instance.LogicOptions.MapId);
            __instance.CheckSettingsDiffs();
            if (ConfigurationValues.CurrentPresetName.Length > 0)
                __instance.RulesPresetText.text = ConfigurationValues.CurrentPresetName;
            else
                __instance.RulesPresetText.text = DestroyableSingleton<TranslationController>.Instance.GetString(GameOptionsManager.Instance.CurrentGameOptions.GetRulesPresetTitle());
        }
        catch { }

        if (GameCode.IntToGameName(AmongUsClient.Instance.GameId) == null) __instance.privatePublicPanelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.LocalButton);
        else if (AmongUsClient.Instance.IsGamePublic) __instance.privatePublicPanelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PublicHeader);
        else __instance.privatePublicPanelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PrivateHeader);
        
        __instance.HostPrivateButton.gameObject.SetActive(!AmongUsClient.Instance.IsGamePublic);
        __instance.HostPublicButton.gameObject.SetActive(AmongUsClient.Instance.IsGamePublic);

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
            ClipboardHelper.PutClipboardString(GameCode.IntToGameName(AmongUsClient.Instance.GameId));

        if (DestroyableSingleton<DiscordManager>.InstanceExists)
        {
            bool active = AmongUsClient.Instance.AmHost && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame && DestroyableSingleton<DiscordManager>.Instance.CanShareGameOnDiscord() && DestroyableSingleton<DiscordManager>.Instance.HasValidPartyID();
            __instance.ShareOnDiscordButton.gameObject.SetActive(active);
        }


        //人数の調整
        __instance.LastPlayerCount = GameData.Instance.PlayerCount;
        string arg = "<color=#FF0000FF>";
        if (__instance.LastPlayerCount > __instance.MinPlayers) arg = "<color=#00FF00FF>";
        if (__instance.LastPlayerCount == __instance.MinPlayers) arg = "<color=#FFFF00FF>";

        int max = 15;
        if (AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame) max = GameManager.Instance.LogicOptions.MaxPlayers;    
        var text = string.Format("{0}{1}/{2}", arg, __instance.LastPlayerCount, max);

        if (__instance.LastPlayerCount < __instance.MinPlayers) text += $" ({__instance.MinPlayers}↑)";

        __instance.PlayerCounter.text = text;
        __instance.PlayerCounter.enableWordWrapping = false;

        bool canStart = __instance.LastPlayerCount >= __instance.MinPlayers;

        canStart &= PlayerControl.AllPlayerControls.GetFastEnumerator().All(p => !p.gameObject.TryGetComponent<UncertifiedPlayer>(out _));

        LastChecked = canStart;
        __instance.StartButton.SetButtonEnableState(canStart);
        ActionMapGlyphDisplay startButtonGlyph = __instance.StartButtonGlyph;
        
        startButtonGlyph?.SetColor(canStart ? Palette.EnabledColor : Palette.DisabledClear);
        
        if (DestroyableSingleton<DiscordManager>.InstanceExists)
        {
            if (AmongUsClient.Instance.AmHost && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame)
                DestroyableSingleton<DiscordManager>.Instance.SetInLobbyHost(__instance.LastPlayerCount, GameManager.Instance.LogicOptions.MaxPlayers, AmongUsClient.Instance.GameId);
            else
                DestroyableSingleton<DiscordManager>.Instance.SetInLobbyClient(__instance.LastPlayerCount, GameManager.Instance.LogicOptions.MaxPlayers, AmongUsClient.Instance.GameId);
        }
    
        if (AmongUsClient.Instance.AmHost)
        {
            if (__instance.startState == GameStartManager.StartingStates.Countdown)
            {
                int num = Mathf.CeilToInt(__instance.countDownTimer);
                __instance.countDownTimer -= Time.deltaTime;
                int num2 = Mathf.CeilToInt(__instance.countDownTimer);
                if (!__instance.GameStartTextParent.activeSelf) SoundManager.Instance.PlaySound(__instance.gameStartSound, false, 1f, null);

                __instance.GameStartTextParent.SetActive(true);
                __instance.GameStartText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameStarting, num2);
                if (num != num2) PlayerControl.LocalPlayer.RpcSetStartCounter(num2);
                if (num2 <= 0) __instance.FinallyBegin();
            }
            else
            {
                __instance.GameStartTextParent.SetActive(false);
                __instance.GameStartText.text = string.Empty;
            }
        }

        return false;
    }
}

file static class GameStartNotification
{
    static public void Notification()
    {
        if (GeneralConfigurations.CurrentGameMode == GameModes.FreePlay) AmongUsUtil.AddLobbyNotification(Language.Translate("notification.lobby.noTitleForFreeplay"), HudManager.Instance.Notifier.disconnectColor);
        else if (GeneralConfigurations.AssignOpToHostOption) AmongUsUtil.AddLobbyNotification(Language.Translate("notification.lobby.noTitleForOP"), HudManager.Instance.Notifier.disconnectColor);

    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
public class GameStartManagerBeginGame
{
    public static bool Prefix(GameStartManager __instance)
    {
        if (!GameStartManagerUpdatePatch.LastChecked) return false;

        if (AmongUsClient.Instance.AmHost)
        {

            if (GeneralConfigurations.CurrentGameMode == GameModes.FreePlay && PlayerControl.AllPlayerControls.Count == 1)
            {
                if (PlayerControl.AllPlayerControls.Count == 1)
                {
                    int num = GeneralConfigurations.NumOfDummiesOption;

                    for (int n = 0; n < num; n++) AmongUsUtil.SpawnDummy();
                }
            }
        }

        GameStartNotification.Notification();

        return true;
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.SetStartCounter))]
public class GameStartManagerSetStartCounterPatch
{
    public static void Prefix(GameStartManager __instance, [HarmonyArgument(0)] sbyte sec)
    {
        if (sec == -1) return;
        if (!__instance.GameStartTextParent.activeSelf) GameStartNotification.Notification();
    }
}


[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CreatePlayer))]
public class RequireHandshakePatch
{
    public static void Postfix(AmongUsClient __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = ManagedEffects.Sequence(__result.WrapToManaged(), ManagedEffects.Action(()=>
        {
            Certification.RequireHandshake();
            PlayerControl.AllPlayerControls.GetFastEnumerator().Do(p => { if (p.PlayerId != p.cosmetics.ColorId) p.SetColor(p.PlayerId); });
        })).WrapToIl2Cpp();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Awake))]
public class SetUpCertificationPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if(LobbyBehaviour.Instance) __instance.gameObject.AddComponent<UncertifiedPlayer>().MyControl = __instance;
    }
}

[HarmonyPatch(typeof(OptionsConsole), nameof(OptionsConsole.CanUse))]
public class OptionsConsoleCanUsePatch
{
    public static void Prefix(OptionsConsole __instance)
    {
        __instance.HostOnly = false;
    }
}

[HarmonyPatch(typeof(OptionsConsole), nameof(OptionsConsole.Use))]
public class OptionsConsoleUsePatch
{
    public static bool Prefix(OptionsConsole __instance)
    {
        if (!__instance.MenuPrefab.TryGetComponent<GameSettingMenu>(out _)) return true;

        __instance.CanUse(PlayerControl.LocalPlayer.Data, out var flag, out _);
        if (!flag) return false;
        
        PlayerControl.LocalPlayer.NetTransform.Halt();

        if (AmongUsClient.Instance.AmHost)
        {
            GameObject gameObject = GameObject.Instantiate<GameObject>(__instance.MenuPrefab);
            gameObject.transform.SetParent(Camera.main.transform, false);
            gameObject.transform.localPosition = __instance.CustomPosition;
            DestroyableSingleton<TransitionFade>.Instance.DoTransitionFade(null, gameObject.gameObject, null);
        }
        else
        {
            Modules.HelpScreen.TryOpenHelpScreen(HelpScreen.HelpTab.Options);
        }

        return false;
    }
}


[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoJoinOnlineGameFromCode))]
public class JoinGameLoadingPatch
{
    public static void Postfix(AmongUsClient __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var overlay = GameObject.Instantiate(TransitionFade.Instance.overlay, null);
        overlay.transform.position = TransitionFade.Instance.overlay.transform.position;

        System.Collections.IEnumerator CoFadeInIf()
        {
            if (AmongUsClient.Instance.ClientId < 0)
            {
                yield return Effects.ColorFade(overlay, Color.black, Color.clear, 0.2f);
                GameObject.Destroy(overlay.gameObject);
            }
        }

        __result = Effects.Sequence(
            Effects.ColorFade(overlay, Color.clear, Color.black, 0.2f),
            ManagedEffects.Action(()=>NebulaManager.Instance.StartCoroutine(HintManager.CoShowHint(0.6f).WrapToIl2Cpp())).WrapToIl2Cpp(),
            __result,
            CoFadeInIf().WrapToIl2Cpp()
        );
        
    }
}

[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Confirm))]
public class CreateGameOptionsLoadingPatch
{
    public static void Postfix(CreateGameOptions __instance)
    {
        NebulaManager.Instance.StartCoroutine(HintManager.CoShowHint(0.6f + 0.2f).WrapToIl2Cpp());
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public class DelayPlayDropshipAmbiencePatch
{
    static private System.Collections.IEnumerator CoDelayPlayWithoutMusic(LobbyBehaviour __instance)
    {
        SoundManager.Instance.StopAllSound();
        yield return new WaitForSeconds(0.5f);
        AudioSource audioSource = SoundManager.Instance.PlayNamedSound("DropShipAmb", __instance.DropShipSound, true, SoundManager.Instance.AmbienceChannel);
        audioSource.loop = true;
        audioSource.pitch = 1.2f;
    }
    public static void Postfix(LobbyBehaviour __instance)
    {
        if (ClientOption.AllOptions[ClientOption.ClientOptionType.PlayLobbyMusic].Value == 0)
        {
            __instance.StopAllCoroutines();
            __instance.StartCoroutine(CoDelayPlayWithoutMusic(__instance).WrapToIl2Cpp());
        }


        var logoHolder = UnityHelper.CreateObject("NebulaLogoHolder", HudManager.Instance.transform, new(-4.15f, 2.75f));
        logoHolder.AddComponent<SortingGroup>();
        var logo = UnityHelper.CreateObject<SpriteRenderer>("NebulaLogo", logoHolder.transform, Vector3.zero);
        logo.sprite = Citations.NebulaOnTheShip.LogoImage!.GetSprite();
        logo.color = new(1f, 1f, 1f, 0.75f);
        logo.transform.localScale = new(0.45f, 0.45f, 1f);

        var versionText = new NoSGUIText(Virial.Media.GUIAlignment.Right, GUI.API.GetAttribute(Virial.Text.AttributeAsset.VersionShower), new RawTextComponent(NebulaPlugin.VisualVersion)) { PostBuilder = t => t.color = new(1f, 1f, 1f, 0.75f) };
        var instantiatedVersionText = versionText.Instantiate(new Virial.Media.Anchor(new(1f,0.5f), new(0f,0f,0f)), new(100f,100f), out _)!;
        instantiatedVersionText.transform.SetParent(logoHolder.transform, false);
        instantiatedVersionText.transform.localPosition += new Vector3(1f, -0.26f, -0.1f);
        

        System.Collections.IEnumerator CoUpdateLogo()
        {
            while (logoHolder)
            {
                logoHolder.SetActive(ClientOption.AllOptions[ClientOption.ClientOptionType.ShowNoSLogoInLobby].Value == 1);
                yield return null;
            }
        }
        __instance.StartCoroutine(CoUpdateLogo().WrapToIl2Cpp());
        GameOperatorManager.Instance!.Register<GameStartEvent>(_ => GameObject.Destroy(logoHolder), Virial.NebulaAPI.CurrentGame!);
    }
}

[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.Awake))]
public class LobbyViewSettingsPanePatch
{
    public static void Postfix(LobbyViewSettingsPane __instance)
    {
        //役職タブを隠す
        __instance.rolesTabButton.gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.Awake))]
public class ChatNotificationPatch
{
    public static void Postfix(ChatNotification __instance)
    {
        NebulaManager.Instance.ScheduleDelayAction(() =>
        {
            __instance.maskArea.gameObject.SetActive(false);
            __instance.transform.GetChild(0).GetChild(0).gameObject.SetActive(false);

            __instance.player.gameObject.AddComponent<SortingGroup>();
            foreach(var renderer in __instance.player.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.gameObject.TryGetComponent<ZOrderedSortingGroup>(out _))
                {
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    var group = renderer.gameObject.AddComponent<ZOrderedSortingGroup>();
                    group.SetConsiderParentsTo(__instance.player.transform);
                }
            }
            var mask = UnityHelper.CreateObject<SpriteMask>("Mask", __instance.player.gameObject.transform, new(0f,1.3f,0f));
            mask.sprite = VanillaAsset.FullScreenSprite;
            mask.transform.localScale = new Vector3(10f, 3f);
        });
    }
}

[HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.SetUp))]
public class ChatNotificationSetUpPatch
{
    public static void Postfix(ChatNotification __instance)
    {
        foreach (var renderer in __instance.player.GetComponentsInChildren<SpriteRenderer>()) renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }
}
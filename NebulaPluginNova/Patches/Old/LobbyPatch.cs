using BepInEx.Unity.IL2CPP.Utils;
using Hazel.Crypto;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections;
using InnerNet;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.Logging;
using Nebula.Roles;
using Rewired.UI.ControlMapper;
using Sentry.Internal.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Lobby;
using Virial.Game;
using static Il2CppSystem.Net.Http.Headers.Parser;

namespace Nebula.Patches;


[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
public class GameStartManagerUpdatePatch
{
    public static bool LastChecked = false;
    public static float ShareOnLobbyTimer = 10f;
    public static bool Prefix(GameStartManager __instance)
    {
        if (!GameData.Instance.AsBoolFast(out var gameData)) return false;
        if (!GameManager.Instance.AsBoolFast(out var gameManager)) return false;

        var auClient = AmongUsLLImpl.AmongUsClientInstance;
        bool isGamePublic = auClient.IsGamePublic;

#if PC
        //公開ルームではスライド使用不可 (不特定多数への画像配信を禁止)
        if (isGamePublic) NebulaGameManager.Instance?.LobbySlideManager.Abandon();
#endif

        __instance.MinPlayers = GeneralConfigurations.CurrentGameMode.MinPlayers;


        TranslationController translation = DestroyableSingleton<TranslationController>.Instance;

        try
        {
            __instance.UpdateMapImage((MapNames)gameManager.LogicOptions.MapId);
            __instance.CheckSettingsDiffs();
            if (ConfigurationValues.CurrentPresetName.Length > 0)
                __instance.RulesPresetText.text = ConfigurationValues.CurrentPresetName;
            else
                __instance.RulesPresetText.text = translation.GetString(GameOptionsManager.Instance.CurrentGameOptions.GetRulesPresetTitle());
        }
        catch { }

        if (GameCode.IntToGameName(auClient.GameId) == null) __instance.privatePublicPanelText.text = translation.GetString(StringNames.LocalButton);
        else if (isGamePublic) __instance.privatePublicPanelText.text = translation.GetString(StringNames.PublicHeader);
        else __instance.privatePublicPanelText.text = translation.GetString(StringNames.PrivateHeader);
        
        __instance.HostPrivateButton.gameObject.SetActive(!isGamePublic);
        __instance.HostPublicButton.gameObject.SetActive(isGamePublic);

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
            ClipboardHelper.PutClipboardString(GameCode.IntToGameName(auClient.GameId));

        if (DestroyableSingleton<DiscordManager>.InstanceExists)
        {
            bool active = auClient.AmHost && auClient.NetworkMode == NetworkModes.OnlineGame && DestroyableSingleton<DiscordManager>.Instance.CanShareGameOnDiscord() && DestroyableSingleton<DiscordManager>.Instance.HasValidPartyID();
            __instance.ShareOnDiscordButton.gameObject.SetActive(active);
        }


        //人数の調整
        __instance.LastPlayerCount = gameData.PlayerCount;
        string arg = "<color=#FF0000FF>";
        if (__instance.LastPlayerCount > __instance.MinPlayers) arg = "<color=#00FF00FF>";
        if (__instance.LastPlayerCount == __instance.MinPlayers) arg = "<color=#FFFF00FF>";

        int max = 24;
        if (auClient.NetworkMode != NetworkModes.LocalGame) max = GameManager.Instance.LogicOptions.MaxPlayers;    
        var text = string.Format("{0}{1}/{2}", arg, __instance.LastPlayerCount, max);

        if (__instance.LastPlayerCount < __instance.MinPlayers) text += $" ({__instance.MinPlayers}↑)";

        __instance.PlayerCounter.text = text;
        __instance.PlayerCounter.enableWordWrapping = false;

        bool canStart = __instance.LastPlayerCount >= __instance.MinPlayers;

        canStart &= PlayerControl.AllPlayerControls.GetFastEnumerator().All(p => !p.gameObject.TryGetComponent<UncertifiedPlayer>(out _));
        canStart &= ModSingleton<ShowUp>.Instance.AllPlayerIsNotAfk;

        LastChecked = canStart;
        __instance.StartButton.SetButtonEnableState(canStart);
        ActionMapGlyphDisplay startButtonGlyph = __instance.StartButtonGlyph;
        if(startButtonGlyph != null) startButtonGlyph?.SetColor(canStart ? Palette.EnabledColor : Palette.DisabledClear);

        if (canStart)
        {
            __instance.StartButton.ChangeButtonText(translation.GetString(StringNames.StartLabel));
            __instance.GameStartTextClient.text = translation.GetString(StringNames.WaitingForHost);
        }
        else
        {
            __instance.StartButton.ChangeButtonText(translation.GetString(StringNames.WaitingForPlayers));
            __instance.GameStartTextClient.text = translation.GetString(StringNames.WaitingForPlayers);
        }

        if (DestroyableSingleton<DiscordManager>.InstanceExists)
        {
            if (auClient.AmHost && auClient.NetworkMode == NetworkModes.OnlineGame)
                DestroyableSingleton<DiscordManager>.Instance.SetInLobbyHost(__instance.LastPlayerCount, gameManager.LogicOptions.MaxPlayers, auClient.GameId);
            else
                DestroyableSingleton<DiscordManager>.Instance.SetInLobbyClient(__instance.LastPlayerCount, gameManager.LogicOptions.MaxPlayers, auClient.GameId);
        }
    
        if (auClient.AmHost)
        {
            if (__instance.startState == GameStartManager.StartingStates.Countdown)
            {
                int num = Mathn.CeilToInt(__instance.countDownTimer);
                __instance.countDownTimer -= Time.deltaTime;
                int num2 = Mathn.CeilToInt(__instance.countDownTimer);
                if (!__instance.GameStartTextParent.activeSelf) AmongUsLLImpl.SoundManagerInstance.PlaySound(__instance.gameStartSound, false, 1f, null);

                __instance.GameStartTextParent.SetActive(true);
                __instance.GameStartText.text = translation.GetString(StringNames.GameStarting, num2);
                if (num != num2)
                {
                    AmongUsLLImpl.LocalPlayer.RpcSetStartCounter(num2);
                    if(num2 == 3) ConfigurationValues.ShareAll(); //設定を共有
                }
                if (num2 <= 0) __instance.FinallyBegin();
            }
            else
            {
                __instance.GameStartTextParent.SetActive(false);
                __instance.GameStartText.text = string.Empty;
            }
        }

        var chatOpen = AmongUsLLImpl.HudManagerInstance.Chat.IsOpenOrOpening;

        if (__instance.LobbyInfoPane.gameObject.activeSelf && chatOpen) __instance.LobbyInfoPane.DeactivatePane();
        __instance.LobbyInfoPane.gameObject.SetActive(!ModSingleton<ShowUp>.Instance.AnyoneShowedUp && !chatOpen);
        __instance.HostInfoPanel.transform.parent.gameObject.SetActive(!ModSingleton<ShowUp>.Instance.AnyoneShowedUp);
        __instance.HostInfoPanel.playerHolder.transform.GetChild(1).gameObject.SetActive(!NebulaInput.SomeUiIsActive);
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

        if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
        {

            if (GeneralConfigurations.CurrentGameMode == GameModes.FreePlay && PlayerControl.AllPlayerControls.Count == 1)
            {
                if (PlayerControl.AllPlayerControls.Count == 1)
                {
                    int num = GeneralConfigurations.NumOfDummiesOption;

                    for (int n = 0; n < num; n++) AmongUsUtil.SpawnDummy();
                }
            }

            Certification.RequireHandshake();
            GameOperatorManager.Instance?.Run<LobbyCountdownStartHostEvent>(new(NebulaGameManager.Instance!.GameParameter));
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
            //Certification.RequireHandshake();
            PlayerControl.AllPlayerControls.GetFastEnumerator().Do(p => { if (p.PlayerId != p.cosmetics.ColorId) p.SetColor(p.PlayerId); });
        })).WrapToIl2Cpp();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Awake))]
public class SetUpCertificationPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        //ゲーム中であればなにもしない
        if (AmongUsLLImpl.TryGetAmongUsClientInstance(out var auClient) && auClient.GameState == InnerNetClient.GameStates.Started) return;
        __instance.gameObject.AddComponent<UncertifiedPlayer>().MyControl = __instance;

        PlayerExtension.SetUpScaler(__instance.gameObject);
    }
}

/*
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
*/

/*
[HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Confirm))]
public class CreateGameOptionsLoadingPatch
{
    public static void Postfix(CreateGameOptions __instance)
    {
        NebulaManager.Instance.StartCoroutine(HintManager.CoShowHint(0.6f + 0.2f).WrapToIl2Cpp());
    }
}
*/

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public class DelayPlayDropshipAmbiencePatch
{
    static private System.Collections.IEnumerator CoDelayPlayWithoutMusic(LobbyBehaviour __instance)
    {
        AmongUsLLImpl.SoundManagerInstance.StopAllSound();
        yield return new WaitForSeconds(0.5f);
        AudioSource audioSource = AmongUsLLImpl.SoundManagerInstance.PlayNamedSound("DropShipAmb", __instance.DropShipSound, true, AmongUsLLImpl.SoundManagerInstance.AmbienceChannel);
        audioSource.loop = true;
        audioSource.pitch = 1.2f;
    }
    public static void Postfix(LobbyBehaviour __instance)
    {
        AmongUsClient.Instance.MinSendInterval = 0.15f; //デフォルトは0.15

        if (ClientOption.AllOptions[ClientOption.ClientOptionType.PlayLobbyMusic].Value == 0)
        {
            __instance.StopAllCoroutines();
            __instance.StartCoroutine(CoDelayPlayWithoutMusic(__instance).WrapToIl2Cpp());
        }


        var logoHolder = UnityHelper.CreateObject("NebulaLogoHolder", HudManager.Instance.transform, new(-4.15f, 2.75f));
        logoHolder.AddComponent<SortingGroup>();
        logoHolder.SetAsUIAspectContent(AspectPosition.EdgeAlignments.LeftTop, new(1.15f, 0.26f));

        var logo = UnityHelper.CreateObject<SpriteRenderer>("NebulaLogo", logoHolder.transform, Vector3.zero);
        logo.sprite = Citations.NebulaOnTheShip.LogoImage!.GetSprite();
        logo.color = new(1f, 1f, 1f, 0.75f);
        logo.transform.localScale = new(0.45f, 0.45f, 1f);

        var versionText = new NoSGUIText(Virial.Media.GUIAlignment.Right, GUI.API.GetAttribute(Virial.Text.AttributeAsset.VersionShower), new RawTextComponent(NebulaPlugin.VisualVersion)) { PostBuilder = t =>
        {
            t.color = new(1f, 1f, 1f, 0.75f);

            var buttonObj = UnityHelper.CreateObject("CopyButton", t.transform, Vector3.zero);
            buttonObj.SetUpButton().OnClick.AddListener(() => {
                ClipboardHelper.PutClipboardString(t.text);
                DebugScreen.Push(Language.Translate("ui.version.copied"), 3f);
            });
            var buttonCollider = buttonObj.AddComponent<BoxCollider2D>();
            buttonCollider.size = t.rectTransform.sizeDelta;
            buttonCollider.isTrigger = true;
        }
        };
        var instantiatedVersionText = versionText.Instantiate(new Virial.Media.Anchor(new(1f,0.5f), new(0f,0f,0f)), new(100f,100f), out _)!;
        instantiatedVersionText.transform.SetParent(logoHolder.transform, false);
        instantiatedVersionText.transform.localPosition += new Vector3(1f, -0.26f, -0.1f);

        System.Collections.IEnumerator CoUpdateLogo()
        {
            while (logoHolder)
            {
                logoHolder.SetActive(ClientOption.GetValue(ClientOption.ClientOptionType.ShowNoSLogoInLobby) == 1);
                yield return null;
            }
        }
        __instance.StartCoroutine(CoUpdateLogo().WrapToIl2Cpp());
        GameOperatorManager.Instance!.Subscribe<LobbyDestroyEvent>(_ => GameObject.Destroy(logoHolder), Virial.NebulaAPI.CurrentGame!);
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

//Lobbyコンソール

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public class MarketplaceConsolePatch
{
    private static IDividedSpriteLoader lobbyConsoleSprite = DividedSpriteLoader.FromResource("Nebula.Resources.LobbyMarketplace.png", 100f, 4, 1);
    public static void Postfix(LobbyBehaviour __instance)
    {
        var leftBox = __instance.transform.FindChild("Leftbox");
        if (leftBox.AsBoolFast())
        {
            var transform = leftBox.transform;
            transform.localPosition = new(-1.51f, 0.2336f, 0f);
            var pos = transform.position;
            pos.z = pos.y / 1000f;
            transform.position = pos;
        }

        var wardrobe = __instance.transform.FindChild("panel_Wardrobe").GetChild(0);
        if(wardrobe.gameObject.TryGetComponent<OptionsConsole>(out var wardrobeConsole))
        {
            MainMenuManagerInstance.SetPrefab(wardrobeConsole.MenuPrefab);

            var marketPlace = GameObject.Instantiate(wardrobeConsole, __instance.transform);
            marketPlace.MenuPrefab = null;
            marketPlace.Outline = null;

            marketPlace.transform.position = new Vector3(-1.44f,0.53f);

            var renderer = marketPlace.gameObject.AddComponent<SpriteRenderer>();
            renderer.material = wardrobeConsole.Outline.material;
            renderer.transform.localScale = new(0.7f, 0.7f, 1f);
            System.Collections.IEnumerator CoAnim()
            {
                var rendererTransform = renderer.transform;
                while (true)
                {
                    rendererTransform.localEulerAngles = new(0, 0, System.Random.Shared.NextSingle() * 360f);
                    renderer.sprite = lobbyConsoleSprite.GetSprite(Mathn.Prob(0.15f) ? 2 : Mathn.Prob(0.2f) ? 3 : Mathn.Prob(0.5f) ? 0 : 1);

                    yield return Effects.Wait(0.2f);
                }
            }
            marketPlace.StartCoroutine(CoAnim().WrapToIl2Cpp());
            marketPlace.Outline = renderer;

            GameOperatorManager.Instance?.RegisterReleasedAction(()=> {
                if (ModSingleton<Marketplace>.Instance) ModSingleton<Marketplace>.Instance.Close();
            }, new GameObjectLifespan(__instance.gameObject));

            var settingsMap = AmongUsLLImpl.HudManagerBridge.UseButtonVanillaSettings;
            var origSettings = settingsMap[ImageNames.WardrobeButton];
            //新しい要素の追加なのでLLは使用しない。
            HudManager.Instance.UseButton.fastUseSettings[(ImageNames)ModImageNames.Marketplace] = new() { ButtonType = origSettings.ButtonType, FontMaterial = origSettings.FontMaterial, Image = origSettings.Image, Text = (StringNames)ModStringNames.Marketplace };
            marketPlace.CustomUseIcon = (ImageNames)ModImageNames.Marketplace;

        }
        else
        {
            NebulaLogger.Instance.Message("Wardrobe console was not found ");
        }

        ClientOption.ChangeAmbientVolumeIfNecessary(false, true);
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

public static class ModStringNames
{
    public const int Marketplace = 20000;
}

public static class ModImageNames
{
    public const int Marketplace = 100;
}

[HarmonyPatch(typeof(OptionsConsole), nameof(OptionsConsole.Use))]
public class OptionsConsoleUsePatch
{
    public static bool Prefix(OptionsConsole __instance)
    {
        if (__instance.MenuPrefab != null && !__instance.MenuPrefab.TryGetComponent<GameSettingMenu>(out _)) return true;

        __instance.CanUse(AmongUsLLImpl.LocalPlayer.Data, out var flag, out _);
        if (!flag) return false;

        AmongUsLLImpl.LocalPlayer.NetTransform.Halt();

        if (__instance.MenuPrefab == null)
        {
            Marketplace.OpenInLobby();
            return false;
        }

        if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
        {
            GameObject gameObject = GameObject.Instantiate<GameObject>(__instance.MenuPrefab);
            gameObject.transform.SetParent(Camera.main.transform, false);
            gameObject.transform.localPosition = __instance.CustomPosition;
            DestroyableSingleton<TransitionFade>.Instance.DoTransitionFade(null, gameObject, null);
        }
        else
        {
            Modules.HelpScreen.TryOpenHelpScreen(HelpScreen.HelpTab.Options);
        }

        return false;
    }
}


[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
public class GlobalCosMismatchShowerPatch
{
    private static IDividedSpriteLoader icons = DividedSpriteLoader.FromResource("Nebula.Resources.GlobalCosButton.png", 100f, 2, 1);
    public static void Postfix(GameStartManager __instance)
    {

        __instance.gameObject.ForEachAllChildren(c => c.layer = LayerExpansion.GetUILayer());

        //過去の未所持データをクリアする
        MoreCosmic.UnacquiredItems.Clear();

        var renderer = UnityHelper.CreateObject<SpriteRenderer>("UnacquiredItemsIcon", __instance.LobbyInfoPane.transform, new(-3.7f, -7f, -1f));
        var rendererObj = renderer.gameObject;
        var rendererTransform = renderer.transform;
        rendererTransform.localScale = new(1.2f, 1.2f, 1.2f);
        renderer.sprite = icons.GetSprite(0);

        var animRenderer = UnityHelper.CreateObject<SpriteRenderer>("Anim", rendererTransform, new(0.2f, 0.2f, -0.1f));
        animRenderer.transform.localScale = Vector3.one;
        animRenderer.sprite = icons.GetSprite(1);

        void UpdateUnacquiredItems() => MoreCosmic.UnacquiredItems.RemoveAll(entry => MarketplaceData.Data.OwningCostumes.Any(c => c.EntryId == entry.id));

        void OpenScreen()
        {
            var window = MetaScreen.GenerateWindow(new(5f, 3.5f), HudManager.Instance.transform, Vector3.zero, true, true, true);
            window.SetWidget(
                GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), "ui.costume.unownedCostume"),
                GUI.API.ScrollView(Virial.Media.GUIAlignment.Center, new(4.8f, 3f), "unacquiredItems", null, out var artifact))
                , out _);
            void UpdateContents()
            {
                artifact.Do(a => a.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    MoreCosmic.UnacquiredItems.Select(item => new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.MarketplaceTitle), GUI.API.RawTextComponent("・" + item.title)) { 
                        OnClickText = (() =>
                        {
                            Marketplace.OpenDetailWindow(false, item.id, HudManager.Instance.transform, ()=> { UpdateUnacquiredItems(); UpdateContents(); });
                        },false),
                        PostBuilder = text =>
                        {
                            var button = text.GetComponent<PassiveButton>();
                            button.OnMouseOver.AddListener(() => text.color = Color.green);
                            button.OnMouseOut.AddListener(() => text.color = Color.white);
                        }
                    })), out _));
            }

            UpdateContents();
        }

        __instance.StartCoroutine(ManagedEffects.Wait(()=> !__instance.HostInfoPanel.content.active, () =>
        {
            __instance.HostInfoPanel.playerHolder.AddComponent<SortingGroup>();
            var layer = __instance.HostInfoPanel.playerHolder.GetComponentInChildren<NebulaCosmeticsLayer>();
            layer.SetSortingProperty(true, 10000f);
            layer.MaskRenderers().Do(r => r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask);
            __instance.HostInfoPanel.playerHolder.transform.SetLocalZ(1f);
            var mask = __instance.HostInfoPanel.playerHolder.GetComponentInChildren<SpriteMask>(true);
            mask.gameObject.AddComponent<SortingGroupOrderFixer>().Initialize(mask, 500);
        }).WrapToIl2Cpp());
        

        var button = rendererObj.SetUpButton(true, renderer);
        button.OnClick.AddListener(OpenScreen);
        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, Language.Translate("ui.costume.missedCostume")));
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        var collider = rendererObj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.3f;

        rendererObj.SetActive(false);

        System.Collections.IEnumerator CoUpdate()
        {
            while (true)
            {
                UpdateUnacquiredItems();
                bool show = MoreCosmic.UnacquiredItems.Count > 0;
                rendererObj.SetActive(show);
                
                //たまに大きくなるアニメーション
                var t = Mathf.Repeat(Time.time, 2.4f);
                animRenderer.transform.localScale = Vector3.one * (1f + Helpers.MountainCurve(Mathn.Clamp01(t / 0.25f), 0.6f));

                yield return null;
            }
        }
        __instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());

        //ロビーで見せたいピックアップチュートリアル
        Tutorial.ShowTutorial(
                    new TutorialBuilder()
                    .BindHistory("stamp")
                    .ShowWhile(() => !HelpScreen.OpenedAnyHelpScreen)
                    .AsSimpleTitledTextWidget(Language.Translate("tutorial.variations.stamp.title"), Language.Translate("tutorial.variations.stamp.caption").ReplaceKeyCode("%KEY%", Virial.Compat.VirtualKeyInput.Stamp)));
    }
}

[HarmonyPatch(typeof(HostInfoPanel), nameof(HostInfoPanel.Update))]
public class HostInfoPanelUpdatePatch
{
    public static void Postfix(HostInfoPanel __instance)
    {
        NetworkedPlayerInfo host = GameData.Instance.GetHost();
        if (host == null || host.IsIncomplete) return;
        string text = ColorUtility.ToHtmlStringRGB(DynamicPalette.PlayerColors[__instance.player.ColorId].ToUnityColor());
        if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
        {
            __instance.playerName.text = string.IsNullOrEmpty(host.PlayerName) ? "..." : string.Concat("<color=#", text, ">", host.PlayerName, "</color>" ) + "  <size=90%><b><font=\"Barlow-BoldItalic SDF\" material=\"Barlow-BoldItalic SDF Outline\">" + DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.HostYouLabel);
        }
        else
        {
            __instance.playerName.text = string.IsNullOrEmpty(host.PlayerName) ? "..." : string.Concat("<color=#", text, ">", host.PlayerName, "</color>" ) + " (" + __instance.player.ColorBlindName + ")";
        }
    }
}


[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ResetStartState))]
public class ResetStartStatePatch
{
    public static void Postfix(GameStartManager __instance)
    {
        SoundManager.Instance.StopSound(__instance.gameStartSound);
    }
}


[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.OnDestroy))]
public class LobbyBehaviourDestroyPatch
{
    public static void Postfix(LobbyBehaviour __instance)
    {
        GameOperatorManager.Instance?.Run<LobbyDestroyEvent>(new());

    }
}
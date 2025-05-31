using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using UnityEngine.UI;
using Virial.Media;

namespace Nebula.Patches;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Awake))]
public static class MainMenuSetUpPatch
{
    static private readonly Image nebulaIconSprite = SpriteLoader.FromResource("Nebula.Resources.NebulaNewsIcon.png", 100f);
    static private readonly Image discordIconSprite = SpriteLoader.FromResource("Nebula.Resources.DiscordIcon.png", 100f);
    static public GameObject? NebulaScreen = null;
    static public GameObject? AddonsScreen = null;
    static public GameObject? VersionsScreen = null;

    static public bool IsLocalGame = false;

    static void Postfix(MainMenuManager __instance)
    {

        __instance.PlayOnlineButton.OnClick.AddListener(() => IsLocalGame = false);
        __instance.playLocalButton.OnClick.AddListener(() => IsLocalGame = true);

        {
            var scaler = __instance.onlineButtonsContainer.GetChild(1);
            scaler.GetChild(0).localPosition = new(-1f, 0.5f, 0f);//CreateLobby
            scaler.GetChild(1).localPosition = new(1.5f,0.5f,0f);//JoinGame
            scaler.GetChild(2).gameObject.SetActive(false);//FindGame
            scaler.GetChild(3).gameObject.SetActive(false);//Line
        }

        var leftPanel = __instance.mainMenuUI.transform.FindChild("AspectScaler").FindChild("LeftPanel");
        leftPanel.GetComponent<SpriteRenderer>().size += new Vector2(0f,0.5f);
        var auLogo = leftPanel.FindChild("Sizer").GetComponent<AspectSize>();
        auLogo.PercentWidth = 0.14f;
        auLogo.DoSetUp();
        auLogo.transform.localPosition += new Vector3(-0.8f, 0.25f, 0f);

        float height = __instance.newsButton.transform.localPosition.y-__instance.myAccountButton.transform.localPosition.y;

        //バニラのパネルからModのパネルに切り替え
        var reworkedPanel = UnityHelper.CreateObject<SpriteRenderer>("ReworkedLeftPanel", leftPanel, new Vector3(0f, height * 0.5f, 0f));
        var oldPanel = leftPanel.GetComponent<SpriteRenderer>();
        reworkedPanel.sprite = oldPanel.sprite;
        reworkedPanel.tileMode= oldPanel.tileMode;
        reworkedPanel.drawMode = oldPanel.drawMode;
        reworkedPanel.size = oldPanel.size;
        oldPanel.enabled = false;

        //CreditsとQuit以外のボタンを上に寄せる
        foreach (var button in __instance.mainButtons.GetFastEnumerator())
            if (Math.Abs(button.transform.localPosition.x) < 0.1f) button.transform.localPosition += new Vector3(0f, height, 0f);
        leftPanel.FindChild("Main Buttons").FindChild("Divider").transform.localPosition += new Vector3(0f, height, 0f);

        var nebulaButton = GameObject.Instantiate(__instance.settingsButton, __instance.settingsButton.transform.parent);
        nebulaButton.transform.localPosition += new Vector3(0f, -height, 0f);
        nebulaButton.gameObject.name = "NebulaButton";
        nebulaButton.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => {
            var icon = obj.transform.FindChild("Icon");
            if (icon != null)
            {
                icon.localScale = new Vector3(1f, 1f, 1f);
                icon.GetComponent<SpriteRenderer>().sprite = nebulaIconSprite.GetSprite();
            }
        }));
        var nebulaPassiveButton = nebulaButton.GetComponent<PassiveButton>();
        nebulaPassiveButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        nebulaPassiveButton.OnClick.AddListener(() =>
        {
            VanillaAsset.PlaySelectSE();
            __instance.ResetScreen();
            NebulaScreen?.SetActive(true);
            __instance.screenTint.enabled = true;
        });
        nebulaButton.transform.FindChild("FontPlacer").GetChild(0).GetComponent<TextTranslatorTMP>().SetModText("title.buttons.nebula");

        NebulaScreen = GameObject.Instantiate(__instance.accountButtons, __instance.accountButtons.transform.parent);
        NebulaScreen.name = "NebulaScreen";
        NebulaScreen.transform.GetChild(0).GetChild(0).GetComponent<TextTranslatorTMP>().SetModText("title.label.nebula");
        __instance.mainButtons.Add(nebulaButton);

        GameObject.Destroy(NebulaScreen.transform.GetChild(4).gameObject);

        var temp = NebulaScreen.transform.GetChild(3);
        int index = 0;
        void SetUpButton(string button,Action clickAction)
        {
            GameObject obj = temp.gameObject;
            if (index > 0) obj = GameObject.Instantiate(obj, obj.transform.parent);

            obj.transform.GetChild(0).GetChild(0).GetComponent<TextTranslatorTMP>().SetModText(button);
            var passiveButton = obj.GetComponent<PassiveButton>();
            passiveButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            passiveButton.OnClick.AddListener(() => {
                VanillaAsset.PlaySelectSE();
                clickAction.Invoke();
            });

            obj.transform.localPosition = new Vector3((index % 2 == 0) ? -1.45f : 1.45f, 0.98f - (index / 2) * 0.59f, 0f);
            obj.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
            index++;
        }

        if (NebulaPlugin.AllowHttpCommunication)
        {
            SetUpButton("title.buttons.update", () =>
            {
                __instance.ResetScreen();
                if (!VersionsScreen) CreateVersionsScreen();
                VersionsScreen?.SetActive(true);
                __instance.screenTint.enabled = true;
            });
        }

        SetUpButton("title.buttons.achievements", () => {
            AchievementViewer.Open(__instance);
        });

        if (NebulaPlugin.AllowHttpCommunication)
        {
            SetUpButton("title.buttons.marketplace", () =>
            {
                Marketplace.Open(__instance);
            });
        }
        
        SetUpButton("title.buttons.addons", () => {
            __instance.ResetScreen();
            if (!AddonsScreen) CreateAddonsScreen();
            AddonsScreen?.SetActive(true);
            __instance.screenTint.enabled = true;
        });
        SetUpButton("title.buttons.developersStudio", () => {
            DevStudio.Open(__instance);
        });

        SetUpButton("title.buttons.stats", () => {
            StatsViewer.Open(__instance);
        });

        if (NebulaPlugin.AllowHttpCommunication)
        {
            SetUpButton("title.buttons.form", () =>
            {
                DevTeamContact.OpenContactWindow(null);
            });
        }

        if (DebugTools.DebugMode)
        {
            SetUpButton("title.buttons.rooms", () =>
            {
                RoomViewer.Open(__instance);
            });
        }


        var discordRenderer = UnityHelper.CreateObject<SpriteRenderer>("DiscordButton", NebulaScreen.transform, new Vector3(2.8f,-1.4f));
        discordRenderer.sprite = discordIconSprite.GetSprite();
        var discordButton = discordRenderer.gameObject.SetUpButton(true, discordRenderer);
        discordButton.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(discordButton, Language.Translate("title.label.discord")));
        discordButton.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(discordButton));
        discordButton.OnClick.AddListener(() => Application.OpenURL(Helpers.ConvertUrl("https://discord.gg/kHNZD4pq9E")));
        discordButton.gameObject.AddComponent<CircleCollider2D>().radius = 0.25f;

        void CreateAddonsScreen()
        {
            AddonsScreen = UnityHelper.CreateObject("Addons", __instance.accountButtons.transform.parent, new Vector3(0, 0, -1f));
            AddonsScreen.transform.localScale = NebulaScreen!.transform.localScale;

            var screen = MetaScreen.GenerateScreen(new Vector2(6.2f, 4.1f), AddonsScreen.transform, new Vector3(-0.1f, 0, 0f), false, false, false);

            TextAttributeOld NameAttribute = new(TextAttributeOld.BoldAttr) { 
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                Size = new Vector2(3.4f,0.3f),
                Alignment = TMPro.TextAlignmentOptions.Left
            };

            TextAttributeOld VersionAttribute = new TextAttributeOld(TextAttributeOld.NormalAttr)
            {
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                Size = new Vector2(0.8f, 0.3f),
                Alignment = TMPro.TextAlignmentOptions.Left
            }.EditFontSize(1.4f, 1f, 1.4f);
            TextAttributeOld AuthorAttribute = new TextAttributeOld(TextAttributeOld.NormalAttr)
            {
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                Size = new Vector2(1.2f, 0.3f),
                Alignment = TMPro.TextAlignmentOptions.Left
            }.EditFontSize(1.4f, 1f, 1.4f);

            TextAttributeOld DescAttribute = new TextAttributeOld(TextAttributeOld.NormalAttr) {
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial, 
                Alignment = TMPro.TextAlignmentOptions.TopLeft,
                Size = new Vector2(5.8f,0.4f),
                FontSize = 1.2f, FontMaxSize = 1.2f, FontMinSize = 0.7f 
            };


            var inner = new MetaWidgetOld();
            foreach (var addon in NebulaAddon.AllAddons)
            {
                if (addon.IsHidden) continue;

                inner.Append(new CombinedWidgetOld(0.5f,
                    new MetaWidgetOld.Image(addon.Icon) { Width = 0.3f, PostBuilder = renderer => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask },
                    new MetaWidgetOld.HorizonalMargin(0.1f),
                    new MetaWidgetOld.Text(NameAttribute) { RawText = addon.AddonName },
                    new MetaWidgetOld.Text(VersionAttribute) { RawText = addon.Version },
                    new MetaWidgetOld.Text(AuthorAttribute) { RawText = "by " + addon.Author })
                { Alignment = IMetaWidgetOld.AlignmentOption.Left });
                inner.Append(new MetaWidgetOld.Text(DescAttribute) { RawText = addon.Description });
            }
            screen.SetWidget(new MetaWidgetOld.ScrollView(new Vector2(6.2f, 4.1f), inner, true) { Alignment = IMetaWidgetOld.AlignmentOption.Center });
        }

        void CreateVersionsScreen()
        {
            VersionsScreen = UnityHelper.CreateObject("Versions", __instance.accountButtons.transform.parent, new Vector3(0, 0, -1f));
            VersionsScreen.transform.localScale = NebulaScreen!.transform.localScale;

            var screen = MetaScreen.GenerateScreen(new Vector2(6.2f, 4.1f), VersionsScreen.transform, new Vector3(-0.1f, 0, 0f), false, false, false);

            TextAttributeOld NameAttribute = new(TextAttributeOld.BoldAttr)
            {
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                Size = new Vector2(2.2f, 0.3f),
                Alignment = TMPro.TextAlignmentOptions.Left
            };

            TextAttributeOld CategoryAttribute = new(TextAttributeOld.BoldAttr)
            {
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                Size = new Vector2(0.8f, 0.3f),
                Alignment = TMPro.TextAlignmentOptions.Center
            };
            CategoryAttribute.EditFontSize(1.2f,0.6f,1.2f);

            TextAttributeOld ButtonAttribute = new(TextAttributeOld.BoldAttr)
            {
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                Size = new Vector2(1f, 0.2f),
                Alignment = TMPro.TextAlignmentOptions.Center
            };

            Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();
            List<ModUpdater.ReleasedInfo>? versions = null;
            MetaWidgetOld staticWidget = new();

            MetaWidgetOld menuWidget = new();
            menuWidget.Append(Enum.GetValues<ModUpdater.ReleasedInfo.ReleaseCategory>(), (category) =>
            new MetaWidgetOld.Button(() => UpdateContents(category), new(TextAttributeOld.BoldAttr) { Size = new(0.95f, 0.28f) }) { TranslationKey = ModUpdater.ReleasedInfo.CategoryTranslationKeys[(int)category] }
                , 1, -1, 0, 0.6f);

            staticWidget.Append(new ParallelWidgetOld(
                new(new MetaWidgetOld.HorizonalMargin(0.1f),0.1f),
                new(menuWidget,1f),
                new(new MetaWidgetOld.HorizonalMargin(0.1f), 0.1f),
                new(new MetaWidgetOld.ScrollView(new Vector2(5f, 4f), new MetaWidgetOld(), true) { Alignment = IMetaWidgetOld.AlignmentOption.Center, InnerRef = innerRef },5f)));
            
            screen.SetWidget(staticWidget);

            innerRef.Value?.SetLoadingWidget();

            void UpdateContents(ModUpdater.ReleasedInfo.ReleaseCategory? category = null)
            {
                if (versions == null) return;

                var inner = new MetaWidgetOld();

                void AutoUpdateContent(string translationKey, Func<bool> predicate, Action onSelected)
                {
                    List<IMetaParallelPlacableOld> placeable = [];

                    placeable.Add(new MetaWidgetOld.Text(CategoryAttribute) { RawText = Language.Translate("version.category.auto") });
                    placeable.Add(new MetaWidgetOld.HorizonalMargin(0.15f));
                    placeable.Add(new MetaWidgetOld.Text(NameAttribute) { TranslationKey = translationKey });
                    placeable.Add(new MetaWidgetOld.HorizonalMargin(0.15f));
                    if(!predicate.Invoke())
                        placeable.Add(new MetaWidgetOld.Button(() => { onSelected.Invoke(); UpdateContents(category);  MetaUI.ShowConfirmDialog(null, new TranslateTextComponent("ui.update.autoUpdate")); }, ButtonAttribute) { TranslationKey = "version.fetching.setAutoUpdate", PostBuilder = (_, renderer, _) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask });
                    else
                    {
                        placeable.Add(new MetaWidgetOld.HorizonalMargin(0.13f));
                        placeable.Add(new MetaWidgetOld.Text(ButtonAttribute) { TranslationKey = "version.fetching.applied" });
                    }
                    
                    inner.Append(new CombinedWidgetOld(0.5f, placeable.ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Left });
                }

                if ((category ?? ModUpdater.ReleasedInfo.ReleaseCategory.Major) == ModUpdater.ReleasedInfo.ReleaseCategory.Major)
                    AutoUpdateContent("version.fetching.autoUpdate.major", () =>
                        (NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.AutoUpdate))?.Value ?? false) &&
                        (NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.UseSnapshot))?.Value ?? false), () => {
                        NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.AutoUpdate))?.Value = true;
                        NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.UseSnapshot))?.Value = false;
                    });
                if ((category ?? ModUpdater.ReleasedInfo.ReleaseCategory.Snapshot) == ModUpdater.ReleasedInfo.ReleaseCategory.Snapshot)
                    AutoUpdateContent("version.fetching.autoUpdate.snapshot", () => 
                        (NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.AutoUpdate))?.Value ?? false) &&
                        (NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.UseSnapshot))?.Value ?? false), () => {
                        NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.AutoUpdate))?.Value = true;
                        NebulaPlugin.GetLoaderConfig<bool>(nameof(NebulaLoader.NebulaLoader.UseSnapshot))?.Value = true;
                    });

                foreach (var version in versions)
                {
                    if (category != null && version.Category != category) continue;

                    try
                    {
                        List<IMetaParallelPlacableOld> placeable = [];
                        placeable.Add(new MetaWidgetOld.Text(CategoryAttribute) { MyText = NebulaGUIWidgetEngine.Instance.TextComponent(ModUpdater.ReleasedInfo.CategoryColors[(int)version.Category], ModUpdater.ReleasedInfo.CategoryTranslationKeys[(int)version.Category]) });
                        placeable.Add(new MetaWidgetOld.HorizonalMargin(0.15f));
                        placeable.Add(new MetaWidgetOld.Text(NameAttribute)
                        {
                            RawText = version.Version!.Replace('_', ' '),
                            PostBuilder = text =>
                            {
                                var button = text.gameObject.SetUpButton(true);
                                button.gameObject.AddComponent<BoxCollider2D>().size = text.rectTransform.sizeDelta;
                                button.OnClick.AddListener(() => Application.OpenURL(Helpers.ConvertUrl("https://github.com/Dolly1016/Nebula/releases/tag/" + version.RawTag)));
                                button.OnMouseOver.AddListener(() =>
                                {
                                    text.color = Color.green;
                                    if(version.Body!=null) NebulaManager.Instance.SetHelpWidget(button, version.Body);
                                });
                                button.OnMouseOut.AddListener(() =>
                                {
                                    text.color = Color.white;
                                    NebulaManager.Instance.HideHelpWidgetIf(button);
                                });
                            }
                        });
                        placeable.Add(new MetaWidgetOld.HorizonalMargin(0.15f));

                        if (version.Epoch == NebulaPlugin.PluginEpoch && version.BuildNum != NebulaPlugin.PluginBuildNum)
                        {
                            placeable.Add(new MetaWidgetOld.Button(() => NebulaManager.Instance.StartCoroutine(version.CoUpdateAndShowDialog().WrapToIl2Cpp()), ButtonAttribute) { TranslationKey = "version.fetching.gainPackage", PostBuilder = (_, renderer, _) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask });
                        }
                        else
                        {
                            placeable.Add(new MetaWidgetOld.HorizonalMargin(0.13f));
                            placeable.Add(new MetaWidgetOld.Text(ButtonAttribute) { TranslationKey = version.Epoch == NebulaPlugin.PluginEpoch ? "version.fetching.current" : "version.fetching.mismatched", });
                        }


                        inner.Append(new CombinedWidgetOld(0.5f, placeable.ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Left });
                    }
                    catch
                    {
                        Debug.Log("[Invalid Tag]" + version.RawTag ?? "NULL");
                    }
                }

                innerRef.Value?.SetWidget(inner);
            }


            NebulaManager.Instance.StartCoroutine(ModUpdater.CoFetchVersionTags((list) =>
            {
                versions = list;
                UpdateContents();
            }).WrapToIl2Cpp());
        }

        foreach (var obj in GameObject.FindObjectsOfType<GameObject>(true)) {
            if (obj.name is "FreePlayButton" or "HowToPlayButton") GameObject.Destroy(obj);
        }

    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.ResetScreen))]
public static class MainMenuClearScreenPatch
{
    public static void Postfix(MainMenuManager __instance)
    {
        if (MainMenuSetUpPatch.NebulaScreen) MainMenuSetUpPatch.NebulaScreen?.SetActive(false);
        if (MainMenuSetUpPatch.AddonsScreen) MainMenuSetUpPatch.AddonsScreen?.SetActive(false);
        if (MainMenuSetUpPatch.VersionsScreen) MainMenuSetUpPatch.VersionsScreen?.SetActive(false);
    }
}

/*
[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
public class ServerVersionPatch
{
    private static bool IsCustomServer()
    {
        return ServerManager.Instance?.CurrentRegion.TranslateName is StringNames.NoTranslation or null;
    }

    static void Postfix(ref int __result)
    {
        //if(!MainMenuSetUpPatch.IsLocalGame && !IsCustomServer()) __result += 25;
        __result += 25;
    }
}

[HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
class IsVersionModdedPatch
{
    static bool Prefix(ref bool __result)
    {
        int broadcastVersion = Constants.GetBroadcastVersion();
        __result = Constants.GetVersionComponents(broadcastVersion).Item4 >= 25;
        return false;
    }
}
*/
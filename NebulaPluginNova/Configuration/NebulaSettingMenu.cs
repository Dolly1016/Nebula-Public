using Il2CppInterop.Runtime.Injection;
using System.Text;
using UnityEngine.Rendering;
using Nebula.Roles;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Virial.Media;
using Virial.Configuration;
using Nebula.Compat;

namespace Nebula.Configuration;

public class NebulaSettingMenu : MonoBehaviour
{
    static NebulaSettingMenu()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaSettingMenu>();
    }

    MetaScreen LeftHolder = null!, MainHolder = null!, RightHolder = null!, SecondScreen = null!, SecondTopScreen = null!;
    GameObject FirstPage = null!, SecondPage = null!;
    //Scroller FirstScroller = null!, SecondScroller = null!;
    Virial.Compat.Artifact<GUIScreen> FirstInnerScreen = null!, SecondInnerScreen = null!;
    TMPro.TextMeshPro SecondTitle = null!;
    ConfigurationTab CurrentTab = null!;

    static public NebulaSettingMenu Instance { get; private set; } = null!;

    public void Start()
    {
        Instance = this;

        CurrentTab = ConfigurationTab.Settings;

        FirstPage = UnityHelper.CreateObject("FirstPage",transform,Vector3.zero);
        LeftHolder = UnityHelper.CreateObject<MetaScreen>("LeftHolder", FirstPage.transform, new Vector3(-4.1f, 0.3f));
        RightHolder = UnityHelper.CreateObject<MetaScreen>("RightHolder", FirstPage.transform, new Vector3(2.5f, 0f));

        var MainHolderParent = UnityHelper.CreateObject("Main", FirstPage.transform, new Vector3(-1.05f, -0.4f));
        MainHolderParent.AddComponent<SortingGroup>();
        var MainHolderMask = UnityHelper.CreateObject<SpriteMask>("Mask",MainHolderParent.transform, Vector3.zero);
        MainHolderMask.sprite = VanillaAsset.FullScreenSprite;
        MainHolderMask.transform.localScale = new Vector3(6f,4.5f);
        MainHolder = UnityHelper.CreateObject<MetaScreen>("MainHolder", MainHolderParent.transform, new Vector3(0f, 0f));
        MainHolder.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += MetaWidgetOld.ScrollView.GetDistHistoryUpdater(() => MainHolder.transform.localPosition.y, "SettingsMenu");

        var firstScroller = new GUIScrollView(Virial.Media.GUIAlignment.Left, new Vector2(4f, 4.5f), null) { ScrollerTag = "RoleSettingFirst" };
        FirstInnerScreen = firstScroller.Artifact;
        MainHolder.SetWidget(firstScroller, new(0.5f, 0.5f), out _);
        //FirstScroller = VanillaAsset.GenerateScroller(new Vector2(4f, 4.5f), MainHolderParent.transform, new Vector3(2.2f, -0.05f, -1f), MainHolder.transform, new FloatRange(0f, 1f),4.6f);
        
        UpdateLeftTab();
        UpdateMainTab();

        SecondPage = UnityHelper.CreateObject("SecondPage", transform, Vector3.zero);
        var SecondParent = UnityHelper.CreateObject("Main", SecondPage.transform, new Vector3(-0.3f, -0.7f));
        SecondParent.AddComponent<SortingGroup>();
        var SecondMask = UnityHelper.CreateObject<SpriteMask>("Mask", SecondParent.transform, Vector3.zero);
        SecondMask.sprite = VanillaAsset.FullScreenSprite;
        SecondMask.transform.localScale = new Vector3(8f, 4.1f);
        SecondScreen = UnityHelper.CreateObject<MetaScreen>("SecondScreen", SecondParent.transform, new Vector3(0f, 0f, -5f));

        var secondScroller = new GUIScrollView(Virial.Media.GUIAlignment.Left, new Vector2(8f, 4.1f), null) { ScrollerTag = "RoleSettingSecond" };
        SecondInnerScreen = secondScroller.Artifact;
        SecondScreen.SetWidget(secondScroller, new(0.5f, 0.5f), out _);
        //SecondScroller = VanillaAsset.GenerateScroller(new Vector2(8f, 4.1f), SecondParent.transform, new Vector3(4.2f, -0.05f, -1f), SecondScreen.transform, new FloatRange(0f, 1f), 4.2f);
        

        //左上タイトルと戻るボタン
        SecondTitle = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, SecondPage.transform);
        TextAttributeOld.TitleAttr.Reflect(SecondTitle);
        SecondTitle.text = "Configuration Title";
        SecondTitle.transform.localPosition = new Vector3(-2.8f, 1.9f, -10f);
        new MetaWidgetOld.Button(() => OpenFirstPage(), new(TextAttributeOld.BoldAttr) { Size = new Vector2(0.4f, 0.28f) }) { RawText = "<<" }.Generate(SecondPage, new Vector2(-4.8f, 1.9f),out _);
        SecondTopScreen = UnityHelper.CreateObject<MetaScreen>("SecondTopScreen", SecondPage.transform, new Vector3(0f, 1.9f, -5f));

        OpenFirstPage();
    }

    private void UpdateLeftTab()
    {
        MetaWidgetOld widget = new();

        widget.Append(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr)) { RawText = Language.Translate("options.gamemode"), Alignment = IMetaWidgetOld.AlignmentOption.Center });
        
        widget.Append(new MetaWidgetOld.Button(() => {
            GeneralConfigurations.GameModeOption.ChangeValue(true);
            UpdateMainTab();
            UpdateLeftTab();
        }, new(TextAttributeOld.BoldAttr) { Size = new(1.5f, 0.3f) }) { Text = GeneralConfigurations.CurrentGameMode.DisplayName, Alignment = IMetaWidgetOld.AlignmentOption.Center});
        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));
        foreach (var tab in ConfigurationTab.AllTab)
        {
            ConfigurationTab copiedTab = tab;
            widget.Append(
                new MetaWidgetOld.Button(() => {
                    CurrentTab = copiedTab;
                    UpdateMainTab();
                }, new(TextAttributeOld.BoldAttr) { Size = new(1.7f, 0.26f) })
                {
                    RawText = tab.DisplayName,
                    Alignment = IMetaWidgetOld.AlignmentOption.Center,
                    PostBuilder = (button, renderer, text) =>
                    {
                        renderer.color = tab.Color.ToUnityColor();
                        button.OnMouseOut.AddListener(() => { renderer.color = tab.Color.ToUnityColor(); });
                    }
                }
                );
        }
        LeftHolder.SetWidget(new Vector2(2f, 3f), widget);
    }

    private static TextAttributeOld RightTextAttr = new(TextAttributeOld.NormalAttr) { FontSize = 1.5f, FontMaxSize = 1.5f, FontMinSize = 0.7f, Size = new(3f, 5f), Alignment = TMPro.TextAlignmentOptions.TopLeft };

    private void UpdateMainTab(bool stay = false)
    {
        if (!stay) MetaWidgetOld.ScrollView.RemoveDistHistory("RoleSettingFirst");
        RightHolder.SetWidget(null);

        MetaWidgetOld widget = new();

        TextAttributeOld mainTextAttr = new(TextAttributeOld.BoldAttr)
        {
            FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
            Size = new Vector2(3.2f, 0.62f),
            FontMaxSize = 2.5f,
            FontSize=2.5f,
            Alignment = TMPro.TextAlignmentOptions.TopLeft
        };
        TextAttributeOld subTextAttr = new(TextAttributeOld.NormalAttr) {
            FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
            Size = new Vector2(3.1f,0.3f),
            FontMaxSize = 1.2f,
            FontMinSize = 0.75f,
            FontSize = 1.2f,
            Alignment = TMPro.TextAlignmentOptions.BottomRight
        };
        TextAttributeOld smallButtonTextAttr = new(TextAttributeOld.BoldAttr)
        {
            FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
            Size = new Vector2(1.5f, 0.27f),
            FontMaxSize = 1.9f,
            FontSize = 1.6f,
            Alignment = TMPro.TextAlignmentOptions.Center
        };

        widget.Append(new MetaWidgetOld.VerticalMargin(0.12f));

        IEnumerable<IConfigurationHolder> holders = ConfigurationHolder.AllHolders.Where(h => h.IsShown && h.Tabs.Test(CurrentTab) && h.GameModes.Test(GeneralConfigurations.CurrentGameMode));
        UnityEngine.Color HolderToColor(IConfigurationHolder h) => h.DisplayOption switch
        {
            ConfigurationHolderState.Inactivated => new(0.2f, 0.2f, 0.2f),
            ConfigurationHolderState.Emphasized => Color.yellow,
            _ => UnityEngine.Color.white
        };
        void ShowOptionsOnRightViewer(IConfigurationHolder h)
        {
            var str = "<size=150%>" + h.Title.GetString().Bold() + "</size>\n" + h.Configurations.Where(c => c.IsShown).Select(a => a?.GetDisplayText()).Where(str => str != null).Join(null, "\n");
            RightHolder.SetWidget(new(2.2f, 3.8f), new MetaWidgetOld.Text(RightTextAttr) { RawText = str });
        }

        if (ClientOption.UseSimpleConfigurationViewerEntry.Value)
        {
            widget.Append(holders,
                h => new MetaWidgetOld.Button(() => OpenSecondaryPage(h), smallButtonTextAttr)
                {
                    RawText = h.Title.GetString(),
                    PostBuilder = (button, renderer, text) =>
                    {
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        renderer.sortingOrder = 10;

                        button.OnMouseOver.AddListener(() =>
                        {
                            ShowOptionsOnRightViewer(h);
                        });
                    },
                    Alignment = IMetaWidgetOld.AlignmentOption.Center,
                    Color = HolderToColor(h)
                }, 2, -1, 0, 0.54f);
        }
        else
        {

            foreach (var holder in holders)
            {
                var copiedHolder = holder;

                widget.Append(new MetaWidgetOld.Button(() => OpenSecondaryPage(copiedHolder), mainTextAttr)
                {
                    RawText = holder.Title.GetString(),
                    PostBuilder = (button, renderer, text) =>
                    {
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        renderer.sortingOrder = 10;

                        text.transform.localPosition += new Vector3(0.03f, -0.03f, 0f);

                        var subTxt = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, renderer.transform);
                        subTextAttr.Reflect(subTxt);
                        subTxt.text = holder.Detail.GetString();
                        subTxt.transform.localPosition = new Vector3(0f, -0.15f, -0.5f);
                        subTxt.sortingOrder = 30;

                        int tagCount = 0;
                        foreach (var tag in holder.Tags)
                        {
                            var tagRenderer = UnityHelper.CreateObject<SpriteRenderer>("Tag", renderer.transform, new(-1.62f + tagCount * 0.25f, 0.4f, -1f));
                            tagRenderer.transform.localScale = new(0.7f, 0.7f, 1f);
                            tagRenderer.sprite = tag.Image.GetSprite();
                            tagRenderer.sortingOrder = 15;
                            tagRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

                            var tagButton = tagRenderer.gameObject.SetUpButton();
                            tagButton.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(tagButton));
                            tagButton.OnMouseOver.AddListener(() => { VanillaAsset.PlayHoverSE(); NebulaManager.Instance.SetHelpWidget(tagButton, tag.Overlay.Invoke()); });
                            var collider = tagButton.gameObject.AddComponent<BoxCollider2D>();
                            collider.isTrigger = true;
                            collider.size = new(0.27f, 0.27f);

                            tagCount++;
                        }

                        button.OnMouseOver.AddListener(() =>
                        {
                            ShowOptionsOnRightViewer(copiedHolder);
                        });
                    },
                    Alignment = IMetaWidgetOld.AlignmentOption.Center,
                    Color = HolderToColor(copiedHolder)
                });
                widget.Append(new MetaWidgetOld.VerticalMargin(0.05f));
            }
        }


        FirstInnerScreen.Do(screen => screen.SetWidget(new NoSGameObjectGUIWrapper(GUIAlignment.Left, widget), out _));
        //FirstScroller.SetYBoundsMax(MainHolder.SetWidget(new Vector2(3.2f, 4.5f), widget) - 4.5f);
    }

    IConfigurationHolder? LastHolder = null;
    IConfigurationHolder? CurrentHolder = null;

    private static TextAttributeOld RelatedButtonAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.1f, 0.29f) };
    public static TextAttributeOld RelatedInsideButtonAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.1f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    public void UpdateSecondaryPage()
    {
        if(LastHolder != CurrentHolder)
        {
            MetaWidgetOld.ScrollView.RemoveDistHistory("RoleSettingSecond");
            LastHolder = CurrentHolder;
        }

        if(CurrentHolder == null) return;

        SecondInnerScreen.Do(screen => screen.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, CurrentHolder.Configurations.Where(c => c.IsShown).Select(c => c.GetEditor().Invoke())), out _));
        //SecondScroller.SetYBoundsMax(SecondScreen.SetWidget(new Vector2(7.8f, 4.1f), CurrentHolder.GetWidget()) - 4.1f);

        //ホルダから上方ボタンを生成
        List<IMetaParallelPlacableOld> topContents = new(CurrentHolder.RelatedInformations.Where(info => info.predicate.Invoke()).Select(info => new MetaWidgetOld.Button(info.onClicked, RelatedButtonAttr) { Text = info.text }));

     
        //関連する役職プリセット
        /*
        if (IConfigPreset.AllPresets.Any(preset => preset.RelatedHolder == CurrentHolder.Id))
        {
            void OpenPresetScreen()
            { 
                var screen = MetaScreen.GenerateWindow(new Vector2(3.8f, 3.2f), HudManager.Instance.transform, Vector3.zero, true, true);

                MetaWidgetOld inner = new();
                inner.Append(IConfigPreset.AllPresets.Where(preset => preset.RelatedHolder == CurrentHolder.Id), (preset) => new MetaWidgetOld.Button(() => { preset.LoadPreset(); UpdateSecondaryPage(); screen.CloseScreen(); }, new(TextAttributeOld.BoldAttr) { Size = new(2.4f, 0.35f) })
                {
                    RawText = preset.DisplayName,
                    Alignment = IMetaWidgetOld.AlignmentOption.Center,
                    PostBuilder = (button, _, _) => {
                        button.OnMouseOver.AddListener(()=>NebulaManager.Instance.SetHelpWidget(button, preset.Detail));
                        button.OnMouseOut.AddListener(()=>NebulaManager.Instance.HideHelpWidgetIf(button));
                    }
                }.SetAsMaskedButton(), 1, -1, 0, 0.64f);

                screen!.SetWidget(new MetaWidgetOld.ScrollView(new(3.8f, 3.1f), inner, true));
            }


            topContents.Add(new MetaWidgetOld.Button(() => OpenPresetScreen(), RelatedButtonAttr) { TranslationKey = "preset.preset" });
        }
        */

        SecondTopScreen.SetWidget(new Vector2(7.8f,0.4f),new CombinedWidgetOld(0.4f, topContents.ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Right});
        SecondTitle.text = CurrentHolder.Title.GetString();
    }

    internal void OpenSecondaryPage(IConfigurationHolder holder) 
    {
        CloseAllPage();
        SecondPage.SetActive(true);

        CurrentHolder = holder;
        UpdateSecondaryPage();
    }

    public void OpenFirstPage()
    {
        CloseAllPage();
        FirstPage.SetActive(true);
        UpdateMainTab();
    }

    public void Update()
    {
        if (FirstPage.active && Input.GetMouseButtonDown(2))
        {
            ClientOption.UseSimpleConfigurationViewerEntry.Value = !ClientOption.UseSimpleConfigurationViewerEntry.Value;
            OpenFirstPage();
        }
    }

    private void CloseAllPage()
    {
        try
        {
            FirstPage.SetActive(false);
            SecondPage.SetActive(false);
        }
        catch { }
    }
}

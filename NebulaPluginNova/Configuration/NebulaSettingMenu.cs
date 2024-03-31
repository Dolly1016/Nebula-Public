using Il2CppInterop.Runtime.Injection;
using System.Text;
using Nebula.Modules;
using Nebula.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using Nebula.Roles;
using Nebula.Behaviour;

namespace Nebula.Configuration;

public class NebulaSettingMenu : MonoBehaviour
{
    static NebulaSettingMenu()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaSettingMenu>();
    }

    MetaScreen LeftHolder = null!, MainHolder = null!, RightHolder = null!, SecondScreen = null!, SecondTopScreen = null!;
    GameObject FirstPage = null!, SecondPage = null!;
    Scroller FirstScroller = null!, SecondScroller = null!;
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
        MainHolder.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += MetaContextOld.ScrollView.GetDistHistoryUpdater(() => MainHolder.transform.localPosition.y, "SettingsMenu");

        FirstScroller = VanillaAsset.GenerateScroller(new Vector2(4f, 4.5f), MainHolderParent.transform, new Vector3(2.2f, -0.05f, -1f), MainHolder.transform, new FloatRange(0f, 1f),4.6f);
        UpdateLeftTab();
        UpdateMainTab();

        SecondPage = UnityHelper.CreateObject("SecondPage", transform, Vector3.zero);
        var SecondParent = UnityHelper.CreateObject("Main", SecondPage.transform, new Vector3(-0.3f, -0.7f));
        SecondParent.AddComponent<SortingGroup>();
        var SecondMask = UnityHelper.CreateObject<SpriteMask>("Mask", SecondParent.transform, Vector3.zero);
        SecondMask.sprite = VanillaAsset.FullScreenSprite;
        SecondMask.transform.localScale = new Vector3(8f, 4.1f);
        SecondScreen = UnityHelper.CreateObject<MetaScreen>("SecondScreen", SecondParent.transform, new Vector3(0f, 0f, -5f));
        SecondScroller = VanillaAsset.GenerateScroller(new Vector2(8f, 4.1f), SecondParent.transform, new Vector3(4.2f, -0.05f, -1f), SecondScreen.transform, new FloatRange(0f, 1f), 4.2f);
        

        //左上タイトルと戻るボタン
        SecondTitle = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, SecondPage.transform);
        TextAttribute.TitleAttr.Reflect(SecondTitle);
        SecondTitle.text = "Configuration Title";
        SecondTitle.transform.localPosition = new Vector3(-2.8f, 1.9f, -10f);
        new MetaContextOld.Button(() => OpenFirstPage(), new(TextAttribute.BoldAttr) { Size = new Vector2(0.4f, 0.28f) }) { RawText = "<<" }.Generate(SecondPage, new Vector2(-4.8f, 1.9f),out _);
        SecondTopScreen = UnityHelper.CreateObject<MetaScreen>("SecondTopScreen", SecondPage.transform, new Vector3(0f, 1.9f, -5f));

        OpenFirstPage();
    }

    private void UpdateLeftTab()
    {
        MetaContextOld context = new();

        context.Append(new MetaContextOld.Text(new(TextAttribute.BoldAttr)) { RawText = Language.Translate("options.gamemode"), Alignment = IMetaContextOld.AlignmentOption.Center });
        
        context.Append(new MetaContextOld.Button(() => {
            GeneralConfigurations.GameModeOption.ChangeValue(true);
            UpdateMainTab();
            UpdateLeftTab();
        }, new(TextAttribute.BoldAttr) { Size = new(1.5f, 0.3f) }) { RawText = Language.Translate(GeneralConfigurations.CurrentGameMode.TranslateKey),Alignment = IMetaContextOld.AlignmentOption.Center});
        context.Append(new MetaContextOld.VerticalMargin(0.2f));
        foreach (var tab in ConfigurationTab.AllTab)
        {
            ConfigurationTab copiedTab = tab;
            context.Append(
                new MetaContextOld.Button(() => {
                    CurrentTab = copiedTab;
                    UpdateMainTab();
                }, new(TextAttribute.BoldAttr) { Size = new(1.7f, 0.26f) })
                {
                    RawText = tab.DisplayName,
                    Alignment = IMetaContextOld.AlignmentOption.Center,
                    PostBuilder = (button, renderer, text) =>
                    {
                        renderer.color = tab.Color;
                        button.OnMouseOut.AddListener(() => { renderer.color = tab.Color; });
                    }
                }
                );
        }
        LeftHolder.SetContext(new Vector2(2f, 3f), context);
    }

    private static TextAttribute RightTextAttr = new(TextAttribute.NormalAttr) { FontSize = 1.5f, FontMaxSize = 1.5f, FontMinSize = 0.7f, Size = new(3f, 5f), Alignment = TMPro.TextAlignmentOptions.TopLeft };

    private void UpdateMainTab(bool stay = false)
    {
        if (!stay) MetaContextOld.ScrollView.RemoveDistHistory("SettingsMenu");
        MainHolder.transform.localPosition = new Vector3(0, MetaContextOld.ScrollView.TryGetDistHistory("SettingsMenu"), 0);
        RightHolder.SetContext(null);

        MetaContextOld context = new();

        TextAttribute mainTextAttr = new(TextAttribute.BoldAttr)
        {
            FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
            Size = new Vector2(3.2f, 0.62f),
            FontMaxSize = 2.5f,
            FontSize=2.5f,
            Alignment = TMPro.TextAlignmentOptions.TopLeft
        };
        TextAttribute subTextAttr = new(TextAttribute.NormalAttr) {
            FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
            Size = new Vector2(3.1f,0.3f),
            FontMaxSize = 1.2f,
            FontMinSize = 0.75f,
            FontSize = 1.2f,
            Alignment = TMPro.TextAlignmentOptions.BottomRight
        };

        widget.Append(new MetaWidgetOld.VerticalMargin(0.12f));

        foreach (var holder in ConfigurationHolder.AllHolders)
        {
            var copiedHolder = holder;

            if (!holder.IsShown || ((holder.TabMask & CurrentTab) == 0) || (holder.GameModeMask & GeneralConfigurations.CurrentGameMode) == 0) continue;

            context.Append(new MetaContextOld.Button(() => OpenSecondaryPage(copiedHolder), mainTextAttr)
            {
                RawText = holder.Title.GetString(),
                PostBuilder = (button, renderer, text) => { 
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    renderer.sortingOrder = 10;

                    text.transform.localPosition += new Vector3(0.03f, -0.03f, 0f);

                    var subTxt = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, renderer.transform);
                    subTextAttr.Reflect(subTxt);
                    subTxt.text = Language.Translate(holder.Id + ".detail");
                    subTxt.transform.localPosition = new Vector3(0f, -0.15f, -0.5f);
                    subTxt.sortingOrder = 30;

                    int tagCount = 0;
                    foreach(var tag in holder.Tags)
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
                        StringBuilder builder = new();
                        copiedHolder.GetShownString(ref builder);
                        RightHolder.SetContext(new(2.2f, 3.8f),new MetaContextOld.Text(RightTextAttr) { RawText = builder.ToString() });
                    });
                },
                Alignment = IMetaContextOld.AlignmentOption.Center,
                Color = (copiedHolder.IsActivated?.Invoke() ?? true) ? Color.white : new Color(0.2f,0.2f,0.2f)
            });
            context.Append(new MetaContextOld.VerticalMargin(0.05f));
        }

        FirstScroller.SetYBoundsMax(MainHolder.SetContext(new Vector2(3.2f, 4.5f), context) - 4.5f);
    }

    ConfigurationHolder? CurrentHolder = null;

    private static TextAttribute RelatedButtonAttr = new TextAttribute(TextAttribute.BoldAttr) { Size = new Vector2(1.1f, 0.29f) };
    private static TextAttribute RelatedInsideButtonAttr = new TextAttribute(TextAttribute.BoldAttr) { Size = new Vector2(1.1f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    public void UpdateSecondaryPage()
    {
        if(CurrentHolder == null) return;

        SecondScroller.SetYBoundsMax(SecondScreen.SetContext(new Vector2(7.8f, 4.1f), CurrentHolder.GetContext()) - 4.1f);

        List<IMetaParallelPlacableOld> topContents = new();

        if(CurrentHolder.RelatedAssignable != null)
        {
            var assignable = CurrentHolder.RelatedAssignable;
            if (assignable is AbstractRole role)
            {
                //Modifierを付与されうるロール

                void OpenFilterScreen(MetaScreen? screen,AbstractRole role)
                {
                    if (!screen)
                        screen = MetaScreen.GenerateWindow(new Vector2(5f, 3.2f), HudManager.Instance.transform, Vector3.zero, true, true);

                    MetaContextOld inner = new();
                    inner.Append(Roles.Roles.AllIntroAssignableModifiers().Where(m => role.CanLoadDefault(m)), (m) => new MetaContextOld.Button(() => { role.ModifierFilter!.ToggleAndShare(m); OpenFilterScreen(screen,role); }, RelatedInsideButtonAttr)
                    {
                        RawText = m.DisplayName.Color(m.RoleColor),
                        PostBuilder = (button, renderer, text) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask,
                        Alignment = IMetaContextOld.AlignmentOption.Center,
                        Color = role.ModifierFilter!.Contains(m) ? new Color(0.24f, 0.24f, 0.24f) : Color.white
                    }, 3, -1, 0, 0.6f);

                    screen!.SetContext(new MetaContextOld.ScrollView(new(5f, 3.1f), inner, true) { ScrollerTag = "roleFilter" });
                }

                if (role.ModifierFilter != null) topContents.Add(new MetaContextOld.Button(() => OpenFilterScreen(null, role), RelatedButtonAttr) { TranslationKey = "options.role.modifierFilter" });
            }else if(assignable is IntroAssignableModifier iam)
            {
                //付与されうるModifier

                void OpenFilterScreen(MetaScreen? screen, IntroAssignableModifier modifier)
                {
                    if (!screen)
                        screen = MetaScreen.GenerateWindow(new Vector2(5f, 3.2f), HudManager.Instance.transform, Vector3.zero, true, true);

                    MetaContextOld inner = new();
                    inner.Append(Roles.Roles.AllRoles.Where(r=>r.ModifierFilter != null && r.CanLoadDefault(modifier)), (role) => new MetaContextOld.Button(() => { role.ModifierFilter!.ToggleAndShare(iam); OpenFilterScreen(screen, modifier); }, RelatedInsideButtonAttr)
                    {
                        RawText = role.DisplayName.Color(role.RoleColor),
                        PostBuilder = (button, renderer, text) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask,
                        Alignment = IMetaContextOld.AlignmentOption.Center,
                        Color = role.ModifierFilter!.Contains(modifier) ? new Color(0.24f, 0.24f, 0.24f) : Color.white
                    }, 3, -1, 0, 0.6f);

                    screen!.SetContext(new MetaContextOld.ScrollView(new(5f, 3.1f), inner, true) { ScrollerTag = "modifierFilter"});
                }

                topContents.Add(new MetaContextOld.Button(() => OpenFilterScreen(null, iam), RelatedButtonAttr) { TranslationKey = "options.role.modifierFilter" });
            }

            foreach (var related in assignable.RelatedOnConfig()) if(related.RelatedConfig != null) topContents.Add(new MetaContextOld.Button(() => OpenSecondaryPage(related.RelatedConfig!), RelatedButtonAttr) { RawText = related.DisplayName.Color(related.RoleColor) });
        }

        if (IConfigPreset.AllPresets.Any(preset => preset.RelatedHolder == CurrentHolder.Id))
        {
            void OpenPresetScreen()
            { 
                var screen = MetaScreen.GenerateWindow(new Vector2(3.8f, 3.2f), HudManager.Instance.transform, Vector3.zero, true, true);

                MetaContextOld inner = new();
                inner.Append(IConfigPreset.AllPresets.Where(preset => preset.RelatedHolder == CurrentHolder.Id), (preset) => new MetaContextOld.Button(() => { preset.LoadPreset(); UpdateSecondaryPage(); screen.CloseScreen(); }, new(TextAttribute.BoldAttr) { Size = new(2.4f, 0.35f) } )
                {
                    RawText = preset.DisplayName,
                    Alignment = IMetaContextOld.AlignmentOption.Center,
                }.SetAsMaskedButton(), 1, -1, 0, 0.64f);

                screen!.SetContext(new MetaContextOld.ScrollView(new(3.8f, 3.1f), inner, true));
            }


            topContents.Add(new MetaContextOld.Button(() => OpenPresetScreen(), RelatedButtonAttr) { TranslationKey = "preset.preset" });
        }

        SecondTopScreen.SetContext(new Vector2(7.8f,0.4f),new CombinedContextOld(0.4f, topContents.ToArray()) { Alignment = IMetaContextOld.AlignmentOption.Right});
        SecondTitle.text = CurrentHolder.Title.GetString();
    }

    private void OpenSecondaryPage(ConfigurationHolder holder) 
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

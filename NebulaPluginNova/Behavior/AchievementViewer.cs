using Il2CppInterop.Runtime.Injection;
using Nebula.Dev;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using TMPro;
using Virial;
using Virial.Media;
using Virial.Text;

namespace Nebula.Behavior;

internal class AchievementViewer : MonoBehaviour
{
    internal enum SortRule
    {
        Categorized,
        GlobalProgress,
    }

    internal record AchievementContent(GameObject Parent, SpriteRenderer Trophy, TMPro.TextMeshPro? HeaderText, TMPro.TextMeshPro MainText, SpriteRenderer Gauge, TMPro.TextMeshPro GaugeText);


    static AchievementViewer() => ClassInjector.RegisterTypeInIl2Cpp<AchievementViewer>();

    private MetaScreen myScreen = null!;

    protected void Close() => MainMenuManagerInstance.Close(this);

    static public void Open(MainMenuManager mainMenu) => MainMenuManagerInstance.Open<AchievementViewer>("AchievementViewer", mainMenu, viewer => viewer.OnShown());
    
    static private List<INebulaAchievement>? rolesAchsCache = null, seasonalAchsCache = null, perkAchsCache = null, costumeAchsCache = null, aeroGuesserAchsCache = null, othersAchsCache = null, innerslothAchsCache = null, sortedAchsCache = null;
    static private void CalcCategorizedAchievements()
    {
        if (rolesAchsCache == null)
        {
            rolesAchsCache = [];
            seasonalAchsCache = [];
            perkAchsCache = [];
            costumeAchsCache = [];
            aeroGuesserAchsCache = [];
            othersAchsCache = [];
            innerslothAchsCache = [];

            foreach (var a in NebulaAchievementManager.AllAchievements)
            {
                if (!a.RelatedRole.IsEmpty())
                    rolesAchsCache.Add(a);
                else if (a.AchievementType().IsEmpty())
                    othersAchsCache!.Add(a);
                else
                {
                    var type = a.AchievementType().First();
                    if (type == AchievementType.Seasonal)
                        seasonalAchsCache!.Add(a);
                    else if (type == AchievementType.Innersloth)
                        innerslothAchsCache!.Add(a);
                    else if (type == AchievementType.Costume)
                        costumeAchsCache!.Add(a);
                    else if (type == AchievementType.Perk)
                        perkAchsCache!.Add(a);
                    else if(type == AchievementType.AeroGuesser)
                        aeroGuesserAchsCache!.Add(a);
                    else
                        othersAchsCache!.Add(a);
                }
            }
        }
    }

    static private void CalcSortedAchievements()
    {
        if(sortedAchsCache == null)
        {
            sortedAchsCache = NebulaAchievementManager.AllAchievements.ToList();
            sortedAchsCache.Sort((a1, a2) =>
            {
                var diff = a2.GlobalProgress - a1.GlobalProgress;
                if (diff > 0f) return 1;
                else if (diff < 0f) return -1;
                return 0;
            });
        }
    }

    private class EdittingCustomAchivemenet
    {

        public INebulaAchievement? Prefix { get; set; }
        public INebulaAchievement? Infix1 { get; set; }
        public INebulaAchievement? Infix2 { get; set; }
        public INebulaAchievement? Suffix { get; set; }

        public EdittingCustomAchivemenet(NebulaAchievementManager.CustomAchievement achievement)
        {
            Prefix = achievement.GetPrefix()?.achievement;
            Infix1 = achievement.GetInfix1()?.achievement;
            Infix2 = achievement.GetInfix2()?.achievement;
            Suffix = achievement.GetSuffix()?.achievement;
        }
        public void ReflectTo(NebulaAchievementManager.CustomAchievement achievement)
        {
            achievement.Update(Prefix, Infix1, Infix2, Suffix);
        }

        public string? GetTranslationKey(int index)
        {
            switch(index){
                case 0:
                    return Prefix?.PrefixTranslationKey;
                case 1:
                    return Infix1?.InfixTranslationKey;
                case 2:
                    return Infix2?.InfixTranslationKey;
                case 3:
                    return Suffix?.SuffixTranslationKey;
            }
            return null;
        }

        public bool CanReflect()
        {
            int num = 0;
            if (Prefix != null) num++;
            if (Infix1 != null) num++;
            if (Infix2 != null) num++;
            if (Suffix != null) num++;
            return num >= 2;
        }

    }
    static private int TrophyFullMask = 0xFFFF;
    public class ViewerArguments
    {
        public SortRule SortRule { get; set; } = SortRule.Categorized;
        public int TrophyMask { get; set; } = TrophyFullMask;
    }
    static public GUIWidget GenerateWidget(float scrollerHeight,float width, ViewerArguments argument, string? scrollerTag = null, bool showTrophySum = true, Predicate<INebulaAchievement>? predicate = null, string? shownText = null, Action? onClicked = null, Action? onUpdated = null, bool showCustomAchievement = true)
    {
        scrollerTag ??= "Achievements";

        var gui = NebulaAPI.GUI;

        var attr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(2f) };
        var groupAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.7f), Size = new(3f, 0.3f) };
        var headerAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeftNonFlexible)) { FontSize = new(1.1f), Size = new(3f,0.15f) };
        var gProgressAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.02f)};
        var detailTitleAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) };
        var detailDetailAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.5f), Size = new(5f, 6f) };

        List<GUIScrollDynamicInnerContent> inner = [];

        
        void AddCustomTitle()
        {
            int gainedAchievements = NebulaAchievementManager.AllAchievements.Count(a => a.IsCleared);
            inner.Add(new(GUIAlignment.Left, new NoSGUIText(GUIAlignment.Left, groupAttr, new TranslateTextComponent("achievement.group.custom")), 0.5f));

            void TryAddContent(NebulaAchievementManager.CustomAchievement a, int requiredAchievements, int minAchievements)
            {
                if(minAchievements > gainedAchievements) return;
                bool isAvailable = requiredAchievements <= gainedAchievements;

                bool isEmpty = a.IsEmpty;

                GUIWidget[] equipOverlay = [
                    new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.2f)),
#if PC
                    new NoSGUIText(GUIAlignment.Left, AttributeAsset.OverlayContent, new LazyTextComponent(() =>
                        NebulaAchievementManager.AmEquipping(a) ?
                        (Language.Translate("achievement.ui.equipped").Color(Color.green).Bold() + "<br>" + Language.Translate("achievement.ui.unsetTitle")) :
                        Language.Translate("achievement.ui.setTitle")))
#elif ANDROID
                    GUI.API.Button(GUIAlignment.Left, AttributeAsset.SmallWideButton, 
                        new LazyTextComponent(() => Language.Translate(NebulaAchievementManager.AmEquipping(a) ? "achievement.ui.unsetTitle.button" : "achievement.ui.setTitle.button")),
                        _ =>
                        {
                            NebulaAchievementManager.SetCustomTitle(a);
                            NebulaManager.Instance.HideHelpWidget();
                        }
                        )
#endif
                    ];


                float height = 0.8f;
                List<GUIWidget> widgets = [
                new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.02f)),
                isAvailable ?
                new NoSGUIText(GUIAlignment.Left, attr, GUI.API.RawTextComponent(isEmpty ? Language.Translate("achievement.title.custom.empty").Color(Color.gray) : a.LocalizedTitle)) { 
                    OverlayWidget = isEmpty ? GUI.API.LocalizedText(GUIAlignment.Left, AttributeAsset.OverlayContent, "achievement.ui.suggestToEdit") : GUI.API.VerticalHolder(GUIAlignment.Left, a.Achievements.Select(a => a.GetOverlayWidget(true, false, false, false,a.IsCleared)).Join(GUI.API.VerticalMargin(0.08f)).Concat(equipOverlay)),
#if PC
                    OnClickText = ((Action)(() => { NebulaAchievementManager.SetCustomTitle(a); VanillaAsset.PlaySelectSE(); onClicked?.Invoke(); }), true), 
#endif
                    PostBuilder = t => t.outlineWidth = 0.12f} :
                new NoSGUIText(GUIAlignment.Left, attr, GUI.API.RawTextComponent(Language.Translate("achievement.title.custom.unavailable").Replace("%NUM%", requiredAchievements.ToString()))) {
                    PostBuilder = t => t.outlineWidth = 0.12f},
                new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.01f)),
                isAvailable ? new GUIModernButton(GUIAlignment.Left, AttributeAsset.OptionsButtonMedium, GUI.API.LocalizedTextComponent("achievement.ui.craft")){ OnClick = _ => OpenEditorWindow() } : null
                ];

                var achievementContent = new VerticalWidgetsHolder(GUIAlignment.Center, widgets);
                GUIWidget aContenxt = new HorizontalWidgetsHolder(GUIAlignment.Left,
                    new VerticalWidgetsHolder(GUIAlignment.Top, GUI.API.VerticalMargin(0.05f),
                        new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(4)), new(0.38f, 0.38f), isAvailable ? Color.white : new UnityEngine.Color(0.2f, 0.2f, 0.2f)) { IsMasked = true }
                        ),
                    new NoSGUIMargin(GUIAlignment.Left, new(0.15f, 0.1f)),
                    achievementContent
                    );

                inner.Add(new(GUIAlignment.Left, aContenxt, height));

                void OpenEditorWindow()
                {
                    var window = MetaScreen.GenerateWindow(new(8.6f, 4.3f), HudManager.InstanceExists ? HudManager.Instance.transform : null, new(0f, 0f, 0f), true, false, false, BackgroundSetting.Modern);
                    var crafting = new EdittingCustomAchivemenet(a);

                    TextAttribute UnmaskedAttr = GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed);
                    TextAttribute HeaderAttr = new(UnmaskedAttr) { Size =  new(1.85f, 0.32f) };
                    TextAttribute ApplyAttr = new(UnmaskedAttr) { Size = new(1.1f, 0.24f) };
                    TextAttribute ButtonAttr = new(GUI.API.GetAttribute(AttributeAsset.OptionsButtonLonger)) { Size = new(1.4f, 0.33f) };
                    void SetWidget(int index)
                    {
                        TextMeshPro headText = null!;
                        List<GUIWidget> header = [];
                        void AddHeader(int myIndex, int requiredAchievements, int minAchievements)
                        {
                            if (minAchievements > gainedAchievements) return;
                            if(requiredAchievements > gainedAchievements)
                            {
                                header.Add(new GUIButton(GUIAlignment.Center, HeaderAttr, GUI.API.RawTextComponent(Language.Translate("achievement.ui.craft.unavailable").Replace("%NUM%", requiredAchievements.ToString()))) { Color = Color.gray, TextMargin = 0.15f });
                            }
                            else
                            {
                                header.Add(new GUIButton(GUIAlignment.Center, HeaderAttr, GUI.API.RawTextComponent(Language.TranslateIfNotNull(crafting.GetTranslationKey(myIndex), null!) ?? Language.Translate("achievement.ui.craft.empty").Color(Color.gray))){ OnClick = _ =>
                                {
                                    SetWidget(myIndex);
                                }, Color = myIndex == index ? Color.yellow : null, PostBuilder = myIndex == index ? t => headText = t : null, TextMargin = 0.15f });
                            }
                        }
                        AddHeader(0, 0, -10);
                        AddHeader(1, 100, 50);
                        AddHeader(2, 200, 150);
                        AddHeader(3, 0, -10);

                        Func<INebulaAchievement, bool> IsAvailableAchievement = index switch
                        {
                            0 => a => a != null && a.IsCleared && a.HasPrefix,
                            3 => a => a != null && a.IsCleared && a.HasSuffix,
                            _ => a => a != null && a.IsCleared && a.HasInfix
                        };
                        Func<INebulaAchievement, string?> ConvertToTranslationKey = index switch
                        {
                            0 => a => a?.PrefixTranslationKey,
                            3 => a => a?.SuffixTranslationKey,
                            _ => a => a?.InfixTranslationKey
                        };
                        INebulaAchievement? firstSelected = index switch
                        {
                            0 => crafting.Prefix,
                            1 => crafting.Infix1,
                            2 => crafting.Infix2,
                            3 => crafting.Suffix,
                            _ => null
                        };

                        var unsetText = Language.Translate("achievement.ui.unset").Color(Color.gray);
                        var selector = GUI.API.ScrollView(GUIAlignment.Center, new(8.4f, 3f), null, new GUIButtonGroup(GUI.API.Arrange(GUIAlignment.Center, NebulaAchievementManager.AllAchievements.Where(IsAvailableAchievement).Prepend(null).Select(a =>
                        {
                            return new GUIModernButton(GUIAlignment.Center, ButtonAttr, GUI.API.RawTextComponent(Language.TranslateIfNotNull(ConvertToTranslationKey(a), unsetText)))
                            {
                                OnClick = button =>
                                {
                                    switch (index)
                                    {
                                        case 0:
                                            crafting.Prefix = a;
                                            break;
                                        case 1:
                                            crafting.Infix1 = a;
                                            break;
                                        case 2:
                                            crafting.Infix2 = a;
                                            break;
                                        case 3:
                                            crafting.Suffix = a;
                                            break;
                                    }
                                    if (headText) headText.text = Language.TranslateIfNotNull(ConvertToTranslationKey.Invoke(a), null) ?? Language.Translate("achievement.ui.craft.empty").Color(Color.gray);
                                },
                                SelectedDefault = a == firstSelected
                            };
                        }), 5)), out _);

                        window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center, GUI.API.HorizontalHolder(GUIAlignment.Center, header), selector, 
                            GUI.API.LocalizedButton(GUIAlignment.Center, ApplyAttr, "achievement.ui.craft.determine", _ =>
                            {
                                if (crafting.CanReflect())
                                {
                                    window.CloseScreen();
                                    crafting.ReflectTo(a);
                                    NebulaAchievementManager.SetCustomTitle(a, false);
                                    onUpdated?.Invoke();
                                    onClicked?.Invoke();
                                }
                                else
                                {
                                    DebugScreen.Push(Language.Translate("achievement.ui.cannotReflect"), 3f);
                                }
                            })), new Vector2(0.5f, 1f), out _);
                    }
                    SetWidget(0);
                    //onClicked
                }
            }

            TryAddContent(NebulaAchievementManager.GetCustomAchievement(0), 50, -10);
            TryAddContent(NebulaAchievementManager.GetCustomAchievement(1), 150, 50);
            TryAddContent(NebulaAchievementManager.GetCustomAchievement(2), 250, 150);
        }
        
        void AddGroup(string? group, IEnumerable<INebulaAchievement> achievements)
        {
            bool first = true;
            foreach (var a in achievements.Where(a => (predicate?.Invoke(a) ?? true) && !a.IsHidden && (((1 << a.Trophy) & argument.TrophyMask) != 0)))
            {
                if (first)
                {
                    if (group != null) inner.Add(new(GUIAlignment.Left, new NoSGUIText(GUIAlignment.Left, groupAttr, new TranslateTextComponent("achievement.group." + group)), 0.5f));
                    first = false;
                }

                List<GUIWidget> widgets = [
                new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.05f)),
                new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()){ PostBuilder = t => t.outlineWidth = 0.22f },
                new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.03f)),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(INebulaAchievement.HiddenComponent)) {
                    OverlayWidget = a.GetOverlayWidget(true, false, true, false,a.IsCleared,true,true),
#if PC
                    OnClickText = (() => { if (a.IsCleared) { NebulaAchievementManager.SetOrToggleTitle(a); VanillaAsset.PlaySelectSE(); onClicked?.Invoke(); } }, true),
#endif
                    PostBuilder = t => t.outlineWidth = 0.12f},
                new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.05f))
                ];

                float height = 0.65f;

                var progress = a.GetDetailWidget();
                if (progress != null)
                {
                    widgets.Add(progress);
                    height += 0.25f;
                }

                var achievementContent = new VerticalWidgetsHolder(GUIAlignment.Center, widgets);


                GUIWidget aContenxt = new HorizontalWidgetsHolder(GUIAlignment.Left,
                    new VerticalWidgetsHolder(GUIAlignment.Center, GUI.API.VerticalMargin(0.05f),
                        new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new(0.38f, 0.38f), a.IsCleared ? Color.white : new UnityEngine.Color(0.2f, 0.2f, 0.2f)) { IsMasked = true }
                        ),
                    new NoSGUIMargin(GUIAlignment.Left, new(0.15f, 0.1f)),
                    achievementContent
                    );

                if (argument.SortRule is SortRule.GlobalProgress)
                {
                    aContenxt = new GUIPercentageBackground(GUIAlignment.Left, aContenxt, a.GlobalProgress, new(0.8f, 0.8f, 0.8f, 0.2f), false) { WithMask = true };
                }
                inner.Add(new(GUIAlignment.Left, aContenxt, height));
            }
        }

        switch (argument.SortRule)
        {
            case SortRule.GlobalProgress:
                CalcSortedAchievements();
                AddGroup(null, sortedAchsCache!);
                break;
            default:
                AddGroup("recently", NebulaAchievementManager.RecentlyCleared);
                if (showCustomAchievement) AddCustomTitle();
                CalcCategorizedAchievements();
                AddGroup("roles", rolesAchsCache!);
                AddGroup("seasonal", seasonalAchsCache!);
                AddGroup("perk", perkAchsCache!);
                AddGroup("costume", costumeAchsCache!);
                AddGroup("aeroGuesser", aeroGuesserAchsCache!);
                AddGroup("others", othersAchsCache!);
                AddGroup("innersloth", innerslothAchsCache!);
                break;
        }
        var scroller = new Nebula.Modules.GUIWidget.GUIScrollDynamicView(GUIAlignment.Center, new(4.7f, scrollerHeight), inner) { ScrollerTag = scrollerTag, WithMask = true };

        if (showTrophySum)
        {
            var cul = NebulaAchievementManager.Aggregate(predicate);
            List<GUIWidget> footerList = new();
            for (int i = 0; i < cul.Length; i++)
            {
                int copiedIndex = i;
                if (footerList.Count != 0) footerList.Add(new NoSGUIMargin(GUIAlignment.Center, new(0.2f, 0f)));

                footerList.Add(new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(copiedIndex)), new(0.5f, 0.5f),
                    ((1 << i) & argument.TrophyMask) != 0 ? Color.white : new(0.25f,0.25f,0.25f,0.95f), _ =>
                    {
                        if (argument.TrophyMask != 1 << copiedIndex)
                            argument.TrophyMask = 1 << copiedIndex;
                        else
                            argument.TrophyMask = TrophyFullMask;
                        onUpdated?.Invoke();
                    }));
                footerList.Add(new NoSGUIMargin(GUIAlignment.Center, new(0.05f, 0f)));
                footerList.Add(new NoSGUIText(GUIAlignment.Left, detailDetailAttr, new RawTextComponent(cul[i].num + "/" + cul[i].max)));
            }
            var footer = new HorizontalWidgetsHolder(GUIAlignment.Center, footerList.ToArray());

            return new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, scroller, new NoSGUIMargin(GUIAlignment.Center, new(0f, 0.15f)), footer,
                new NoSGUIText(GUIAlignment.Center, detailDetailAttr, new RawTextComponent((shownText ?? Language.Translate(predicate != null ? "achievement.ui.shown" : "achievement.ui.allAchievements")) + ": " + cul.Sum(c => c.num) + "/" + cul.Sum(c => c.max))))
            { FixedWidth = width };
        }
        else
        {
            return scroller;
        }
    }

    public void OnShown() {
        var gui = NebulaAPI.GUI;

        var title = new NoSGUIText(GUIAlignment.Left, gui.GetAttribute(Virial.Text.AttributeAsset.OblongHeader), new TranslateTextComponent("achievement.ui.title"));

        gameObject.SetActive(true);
        ViewerArguments args = new();
        void SetWidget()
        {
            myScreen.SetWidget(new Modules.GUIWidget.VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, title, GenerateWidget(3.85f, 9f, args, onUpdated: SetWidget)), out _);
        }
        SetWidget();

    }

    public void Awake()
    {
        myScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close());
        myScreen.SetBorder(new(9f, 5.5f));
    }
}
    
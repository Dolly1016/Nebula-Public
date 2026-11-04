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
    
    static private List<INebulaAchievement>? rolesAchsCache = null, seasonalAchsCache = null, perkAchsCache = null, costumeAchsCache = null, aeroGuesserAchsCache = null, paintQuizAchsCache = null, othersAchsCache = null, innerslothAchsCache = null, sortedAchsCache = null;
    static private void CalcCategorizedAchievements()
    {
        if (rolesAchsCache == null)
        {
            rolesAchsCache = [];
            seasonalAchsCache = [];
            perkAchsCache = [];
            costumeAchsCache = [];
            aeroGuesserAchsCache = [];
            paintQuizAchsCache = [];
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
                    else if (type == AchievementType.AeroGuesser)
                        aeroGuesserAchsCache!.Add(a);
                    else if (type == AchievementType.PaintQuiz)
                        paintQuizAchsCache!.Add(a);
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
        public INebulaAchievement?[] Parts { get; } = new INebulaAchievement?[4];
        public TitlePartType[] Types { get; } = new TitlePartType[4];

        static public TitlePartType SlotType(int slot) => NebulaAchievementManager.CustomAchievement.SlotType(slot);

        public EdittingCustomAchivemenet(NebulaAchievementManager.CustomAchievement achievement)
        {
            for (int i = 0; i < 4; i++)
            {
                var tip = achievement.Get(i);
                Parts[i] = tip?.achievement;
                Types[i] = tip?.partType ?? SlotType(i);
            }
        }
        public void ReflectTo(NebulaAchievementManager.CustomAchievement achievement)
        {
            achievement.Update(Enumerable.Range(0, 4).Select(i => (Parts[i], Types[i])).ToArray());
        }

        public void Set(int slot, INebulaAchievement? achievement, TitlePartType type)
        {
            Parts[slot] = achievement;
            Types[slot] = achievement == null ? SlotType(slot) : type;
        }

        public string? GetTranslationKey(int index) => Parts[index]?.GetPartTranslationKey(Types[index]);

        /// <summary>その枠でのりを使っているか</summary>
        public bool IsGlued(int slot) => Parts[slot] != null && Types[slot] != SlotType(slot);
        /// <summary>のりを使っている枠の数</summary>
        public int UsedGlues => Enumerable.Range(0, 4).Count(IsGlued);

        public bool CanReflect(int maxGlues)
        {
            return Parts.Count(p => p != null) >= 2 && UsedGlues <= maxGlues;
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
            int gainedAchievements = NebulaAchievementManager.AllAchievements.Count(a => a.IsCleared && !a.IsAddonTitle);
            //のり報酬(定義の"glue")を持つ称号の獲得数だけ、のりを使える
            int gainedGlues = NebulaAchievementManager.AllAchievements.Count(a => a.IsCleared && a.HasGlue);

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
                        (Language.Translate("achievement.ui.equipped").Color(VColor.Green).Bold() + "<br>" + Language.Translate("achievement.ui.unsetTitle")) :
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
                new NoSGUIText(GUIAlignment.Left, attr, GUI.API.RawTextComponent(isEmpty ? Language.Translate("achievement.title.custom.empty").Color(Virial.Color.Gray) : a.LocalizedTitle)) { 
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
                        new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(4)), new(0.38f, 0.38f), isAvailable ? VColor.White : new VColor(0.2f, 0.2f, 0.2f)) { IsMasked = true }
                        ),
                    new NoSGUIMargin(GUIAlignment.Left, new(0.15f, 0.1f)),
                    achievementContent
                    );

                inner.Add(new(GUIAlignment.Left, aContenxt, height));

                void OpenEditorWindow()
                {
                    var window = MetaScreen.GenerateWindow(new(8.6f, 4.3f), HudManager.InstanceExists ? HudManager.Instance.transform : null, new(0f, 0f, 0f), true, false, false, BackgroundSetting.Modern);
                    var crafting = new EdittingCustomAchivemenet(a);

                    TextAttribute ApplyAttr = new(GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed)) { Size = new(1.1f, 0.24f) };
                    TextAttribute PartAttr = GUITitlePart.ScaledAttribute(1.4f);
                    TextAttribute GlueCountAttr = new(GUI.API.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) };
                    TextAttribute GlueCaptionAttr = new(GUI.API.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(1.4f) };
                    VColor EditingSlotColor = new(255, 235, 140);
                    VColor LockedSlotColor = new(120, 120, 120);
                    TitlePartType[] AllPartTypes = [TitlePartType.Prefix, TitlePartType.Infix, TitlePartType.Suffix];

                    //選択状態が変わるたびに作り直す。スクロール位置はタグで保持される。
                    void SetWidget(int index)
                    {
                        var slotType = EdittingCustomAchivemenet.SlotType(index);
                        int remainingGlues = gainedGlues - crafting.UsedGlues;

                        //各枠。噛み合ったひと続きのパーツとして並べ、編集中の枠は色付きで表示する。
                        List<TitlePartEntry> header = [];
                        void AddHeader(int myIndex, int requiredAchievements, int minAchievements)
                        {
                            if (minAchievements > gainedAchievements) return;
                            var mySlotType = EdittingCustomAchivemenet.SlotType(myIndex);
                            if (requiredAchievements > gainedAchievements)
                            {
                                header.Add(new(GUI.API.RawTextComponent(Language.Translate("achievement.ui.craft.unavailable").Replace("%NUM%", requiredAchievements.ToString())), mySlotType) { BackgroundColor = LockedSlotColor });
                            }
                            else
                            {
                                header.Add(new(GUI.API.RawTextComponent(Language.TranslateIfNotNull(crafting.GetTranslationKey(myIndex), null!) ?? Language.Translate("achievement.ui.craft.empty").Color(VColor.Gray)), crafting.Parts[myIndex] != null ? crafting.Types[myIndex] : mySlotType)
                                {
                                    BackgroundColor = myIndex == index ? EditingSlotColor : null,
                                    OnClick = _ => SetWidget(myIndex)
                                });
                            }
                        }
                        AddHeader(0, 0, -10);
                        AddHeader(1, 100, 50);
                        AddHeader(2, 200, 150);
                        AddHeader(3, 0, -10);

                        //残りのり数
                        var headerWidget = GUI.API.HorizontalHolder(GUIAlignment.Center,
                            new GUITitlePartSequence(GUIAlignment.Center, PartAttr, header),
                            GUI.API.HorizontalMargin(0.25f),
                            new NoSGUIImage(GUIAlignment.Center, GUITitlePart.GlueIcon, new(0.42f, 0.42f), overlay: GUI.API.LocalizedText(GUIAlignment.Left, AttributeAsset.OverlayContent, "achievement.ui.craft.glue.detail")),
                            new NoSGUIText(GUIAlignment.Center, GlueCountAttr, GUI.API.RawTextComponent("x" + remainingGlues)));

                        var currentSelected = crafting.Parts[index];
                        var currentType = crafting.Types[index];
                        var unsetText = Language.Translate("achievement.ui.unset").Color(VColor.Gray);

                        //選択肢。パーツはこの枠の種類として使うときの形で表示し、選択中のパーツには右上にチェックマークが付く。
                        GUIWidget PartButton(INebulaAchievement? achievement, TitlePartType partType) => new GUITitlePart(GUIAlignment.Center, PartAttr, GUI.API.RawTextComponent(Language.TranslateIfNotNull(achievement?.GetPartTranslationKey(partType), unsetText)), partType, slotType)
                        {
                            Selected = achievement == currentSelected && (achievement == null || partType == currentType),
                            IsMasked = true,
                            OnClick = _ =>
                            {
                                crafting.Set(index, achievement, partType);
                                SetWidget(index);
                            }
                        };

                        List<GUIWidget> selectorContents = [
                            GUI.API.Arrange(GUIAlignment.Center, NebulaAchievementManager.AllAchievements.Where(a => a.IsCleared && a.HasPart(slotType)).Prepend(null).Select(a => PartButton(a, slotType)), 5)
                            ];

                        //のりが使えるうちは、他の種類のパーツものり付きで並べる(この枠で既にのりを使っている場合は付け替えられる)
                        if (remainingGlues > 0 || crafting.IsGlued(index))
                        {
                            var gluedParts = AllPartTypes.Where(t => t != slotType).SelectMany(t => NebulaAchievementManager.AllAchievements.Where(a => a.IsCleared && a.HasPart(t)).Select(a => PartButton(a, t))).ToArray();
                            if (gluedParts.Length > 0)
                            {
                                selectorContents.Add(GUI.API.VerticalMargin(0.15f));
                                selectorContents.Add(GUI.API.HorizontalHolder(GUIAlignment.Left,
                                    new NoSGUIImage(GUIAlignment.Center, GUITitlePart.GlueIcon, new(0.32f, 0.32f)) { IsMasked = true },
                                    GUI.API.HorizontalMargin(0.05f),
                                    new NoSGUIText(GUIAlignment.Center, GlueCaptionAttr, GUI.API.LocalizedTextComponent("achievement.ui.craft.glued"))));
                                selectorContents.Add(GUI.API.Arrange(GUIAlignment.Center, gluedParts, 5));
                            }
                        }

                        var selector = GUI.API.ScrollView(GUIAlignment.Center, new(8.4f, 3f), "TitleCraft" + index, GUI.API.VerticalHolder(GUIAlignment.Center, selectorContents), out _);

                        window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center, headerWidget, GUI.API.VerticalMargin(0.05f), selector,
                            GUI.API.LocalizedButton(GUIAlignment.Center, ApplyAttr, "achievement.ui.craft.determine", _ =>
                            {
                                if (crafting.CanReflect(gainedGlues))
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
                        new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new(0.38f, 0.38f), a.IsCleared ? VColor.White : new VColor(0.2f, 0.2f, 0.2f)) { IsMasked = true }
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
                AddGroup("paintQuiz", paintQuizAchsCache!);
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
                    ((1 << i) & argument.TrophyMask) != 0 ? VColor.White : new(0.25f,0.25f,0.25f,0.95f), _ =>
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
    
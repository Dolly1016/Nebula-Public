using Il2CppInterop.Runtime.Injection;
using Nebula.Dev;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using Steamworks;
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
    
    static private List<INebulaAchievement>? rolesAchsCache = null, seasonalAchsCache = null, perkAchsCache = null, costumeAchsCache = null, othersAchsCache = null, innerslothAchsCache = null, sortedAchsCache = null;
    static private void CalcCategorizedAchievements()
    {
        if (rolesAchsCache == null)
        {
            rolesAchsCache = [];
            seasonalAchsCache = [];
            perkAchsCache = [];
            costumeAchsCache = [];
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

    static public GUIWidget GenerateWidget(float scrollerHeight,float width, string? scrollerTag = null, bool showTrophySum = true, Predicate<INebulaAchievement>? predicate = null, string? shownText = null, Action<INebulaAchievement>? onClicked = null, SortRule sortRule = SortRule.Categorized)
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
        void AddGroup(string? group, IEnumerable<INebulaAchievement> achievements)
        {
            bool first = true;
            foreach (var a in achievements.Where(a => (predicate?.Invoke(a) ?? true) && !a.IsHidden))
            {
                if (first)
                {
                    if(group != null) inner.Add(new(GUIAlignment.Left, new NoSGUIText(GUIAlignment.Left, groupAttr, new TranslateTextComponent("achievement.group." + group)), 0.5f));
                    first = false;
                }

                List<GUIWidget> widgets = [
                new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.05f)),
                new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()){ PostBuilder = t => t.outlineWidth = 0.22f },
                new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.03f)),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(INebulaAchievement.HiddenComponent)) { OverlayWidget = a.GetOverlayWidget(true, false, true, false,a.IsCleared), OnClickText = (() => { if (a.IsCleared) { NebulaAchievementManager.SetOrToggleTitle(a); VanillaAsset.PlaySelectSE(); onClicked?.Invoke(a); } }, true), PostBuilder = t => t.outlineWidth = 0.12f},
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

                if(sortRule is SortRule.GlobalProgress)
                {
                    aContenxt = new GUIPercentageBackground(GUIAlignment.Left, aContenxt, a.GlobalProgress, new(0.8f, 0.8f, 0.8f, 0.2f), false){ WithMask = true };
                }
                inner.Add(new(GUIAlignment.Left, aContenxt, height));
            }
        }

        switch (sortRule)
        {
            case SortRule.GlobalProgress:
                CalcSortedAchievements();
                AddGroup(null, sortedAchsCache!);
                break;
            default:
                AddGroup("recently", NebulaAchievementManager.RecentlyCleared);
                CalcCategorizedAchievements();
                AddGroup("roles", rolesAchsCache!);
                AddGroup("seasonal", seasonalAchsCache!);
                AddGroup("perk", perkAchsCache!);
                AddGroup("costume", costumeAchsCache!);
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

                footerList.Add(new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(copiedIndex)), new(0.5f, 0.5f)));
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
        myScreen.SetWidget(new Modules.GUIWidget.VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, title, GenerateWidget(3.85f, 9f)), out _);

    }

    public void Awake()
    {
        myScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close());
        myScreen.SetBorder(new(9f, 5.5f));
    }
}
    
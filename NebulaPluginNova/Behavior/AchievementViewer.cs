using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.GUIWidget;
using Steamworks;
using Virial;
using Virial.Media;
using Virial.Text;

namespace Nebula.Behavior;

internal class AchievementViewer : MonoBehaviour
{
    static AchievementViewer() => ClassInjector.RegisterTypeInIl2Cpp<AchievementViewer>();

    private MetaScreen myScreen = null!;

    protected void Close() => MainMenuManagerInstance.Close(this);

    static public void Open(MainMenuManager mainMenu) => MainMenuManagerInstance.Open<AchievementViewer>("AchievementViewer", mainMenu, viewer => viewer.OnShown());
    

    static public GUIWidget GenerateWidget(float scrollerHeight,float width, string? scrollerTag = null, bool showTrophySum = true, Predicate<INebulaAchievement>? predicate = null, string? shownText = null, Action<INebulaAchievement>? onClicked = null)
    {
        scrollerTag ??= "Achievements";

        var gui = NebulaAPI.GUI;

        List<GUIWidget> inner = new();
        var holder = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, inner);
        var attr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };
        var groupAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.6f) };
        var headerAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.1f) };
        var detailTitleAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) };
        var detailDetailAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.5f), Size = new(5f, 6f) };


        void AddGroup(string group, IEnumerable<INebulaAchievement> achievements)
        {
            bool first = true;
            foreach (var a in achievements.Where(a => (predicate?.Invoke(a) ?? true) && !a.IsHidden))
            {
                if (first)
                {
                    inner.Add(new NoSGUIText(GUIAlignment.Left, groupAttr, new TranslateTextComponent("achievement.group." + group)));
                    first = false;
                }

                if (inner.Count != 0) inner.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.07f)));

                List<GUIWidget> widgets = new() {
                new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.12f)),
                new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()),
                new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.12f)),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(INebulaAchievement.HiddenComponent)) { OverlayWidget = a.GetOverlayWidget(true, false, true, false,a.IsCleared), OnClickText = (() => { if (a.IsCleared) { NebulaAchievementManager.SetOrToggleTitle(a); VanillaAsset.PlaySelectSE(); onClicked?.Invoke(a); } }, true) }};
                var progress = a.GetDetailWidget();
                if (progress != null) widgets.Add(progress);

                var achievementContent = new VerticalWidgetsHolder(GUIAlignment.Center, widgets);


                var aContenxt = new HorizontalWidgetsHolder(GUIAlignment.Left,
                    new VerticalWidgetsHolder(GUIAlignment.Center, GUI.API.VerticalMargin(0.05f),
                        new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new(0.38f, 0.38f), a.IsCleared ? Color.white : new UnityEngine.Color(0.2f, 0.2f, 0.2f)) { IsMasked = true }
                        ),
                    new NoSGUIMargin(GUIAlignment.Left, new(0.15f, 0.1f)),
                    achievementContent
                    );
                inner.Add(aContenxt);
            }
        }

        AddGroup("recently", NebulaAchievementManager.RecentlyCleared);
        AddGroup("roles", NebulaAchievementManager.AllAchievements.Where(a => !a.RelatedRole.IsEmpty()));
        AddGroup("seasonal", NebulaAchievementManager.AllAchievements.Where(a => a.RelatedRole.IsEmpty() && !a.AchievementType().IsEmpty() && a.AchievementType().First() == AchievementType.Seasonal));
        AddGroup("perk", NebulaAchievementManager.AllAchievements.Where(a => a.RelatedRole.IsEmpty() && !a.AchievementType().IsEmpty() && a.AchievementType().First() == AchievementType.Perk));
        AddGroup("costume", NebulaAchievementManager.AllAchievements.Where(a => a.RelatedRole.IsEmpty() && !a.AchievementType().IsEmpty() && a.AchievementType().First() == AchievementType.Costume));
        AddGroup("others", NebulaAchievementManager.AllAchievements.Where(a => a.RelatedRole.IsEmpty() && (a.AchievementType().IsEmpty() || (!a.AchievementType().IsEmpty() && (a.AchievementType().First() == AchievementType.Secret || a.AchievementType().First() == AchievementType.Challenge)))));
        AddGroup("innersloth", NebulaAchievementManager.AllAchievements.Where(a => a.RelatedRole.IsEmpty() && !a.AchievementType().IsEmpty() && a.AchievementType().First() == AchievementType.Innersloth));
       
        var scroller = new Nebula.Modules.GUIWidget.GUIScrollView(GUIAlignment.Center, new(4.7f, scrollerHeight), holder) { ScrollerTag = scrollerTag };

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
    
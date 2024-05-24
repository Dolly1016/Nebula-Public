using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.GUIWidget;
using Virial;
using Virial.Media;
using Virial.Text;

namespace Nebula.Behaviour;

internal class AchievementViewer : MonoBehaviour
{
    static AchievementViewer() => ClassInjector.RegisterTypeInIl2Cpp<AchievementViewer>();
    static public MainMenuManager? MainMenu;

    private MetaScreen myScreen = null!;

    protected void Close()
    {
        TransitionFade.Instance.DoTransitionFade(gameObject, null!, () => MainMenu?.mainMenuUI.SetActive(true), () => GameObject.Destroy(gameObject));
    }

    static public void Open(MainMenuManager mainMenu)
    {
        MainMenu = mainMenu;

        var obj = UnityHelper.CreateObject<AchievementViewer>("AchievementViewer", Camera.main.transform, new Vector3(0, 0, -30f));
        TransitionFade.Instance.DoTransitionFade(null!, obj.gameObject, () => { mainMenu.mainMenuUI.SetActive(false); }, () => { obj.OnShown(); });
    }

    static public GUIWidget GenerateWidget(float scrollerHeight,float width, string? scrollerTag = null, bool showTrophySum = true, Predicate<AbstractAchievement>? predicate = null)
    {
        scrollerTag ??= "Achievements";

        var gui = NebulaAPI.GUI;

        List<GUIWidget> inner = new();
        var holder = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, inner);
        var attr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };
        var headerAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.1f) };
        var detailTitleAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) };
        var detailDetailAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.5f), Size = new(5f, 6f) };

        foreach (var a in NebulaAchievementManager.AllAchievements.Where(a => predicate?.Invoke(a) ?? true))
        {
            if (a.IsHidden) continue;

            if (inner.Count != 0) inner.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.08f)));

            List<GUIWidget> widgets = new() {
                new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.12f)),
                new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()),
                new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.12f)),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(AbstractAchievement.HiddenComponent)) { OverlayWidget = a.GetOverlayWidget(true, false, true,false,a.IsCleared), OnClickText = (() => { if (a.IsCleared) { NebulaAchievementManager.SetOrToggleTitle(a); VanillaAsset.PlaySelectSE(); } }, true) }
            };
            var progress = a.GetDetailWidget();
            if (progress != null) widgets.Add(progress);

            var achievementContent = new VerticalWidgetsHolder(GUIAlignment.Center, widgets);


            var aContenxt = new HorizontalWidgetsHolder(GUIAlignment.Left,
                new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new(0.38f, 0.38f), a.IsCleared ? Color.white : new UnityEngine.Color(0.2f, 0.2f, 0.2f)) { IsMasked = true },
                new NoSGUIMargin(GUIAlignment.Left, new(0.15f, 0.1f)),
                achievementContent
                );
            inner.Add(aContenxt);
        }

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
                new NoSGUIText(GUIAlignment.Center, detailDetailAttr, new RawTextComponent(Language.Translate(predicate != null ? "achievement.ui.shown" : "achievement.ui.allAchievements") + ": " + cul.Sum(c => c.num) + "/" + cul.Sum(c => c.max))))
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
        myScreen.SetWidget(new Modules.GUIWidget.VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, title, GenerateWidget(4f, 9f)), out _);

    }

    public void Awake()
    {
        if (MainMenu != null)
        {
            var backBlackPrefab = MainMenu.playerCustomizationPrefab.transform.GetChild(1);
            GameObject.Instantiate(backBlackPrefab.gameObject, transform);
            var backGroundPrefab = MainMenu.playerCustomizationPrefab.transform.GetChild(2);
            var backGround = GameObject.Instantiate(backGroundPrefab.gameObject, transform);
            GameObject.Destroy(backGround.transform.GetChild(2).gameObject);

            var closeButtonPrefab = MainMenu.playerCustomizationPrefab.transform.GetChild(0).GetChild(0);
            var closeButton = GameObject.Instantiate(closeButtonPrefab.gameObject, transform);
            GameObject.Destroy(closeButton.GetComponent<AspectPosition>());
            var button = closeButton.GetComponent<PassiveButton>();
            button.gameObject.SetActive(true);
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener(() => Close());
            button.transform.localPosition = new Vector3(-4.9733f, 2.6708f, -50f);
        }

        myScreen = UnityHelper.CreateObject<MetaScreen>("Screen", transform, new Vector3(0, -0.1f, -10f));
        myScreen.SetBorder(new(9f, 5.5f));
    }
}
    
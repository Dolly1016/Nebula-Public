﻿using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.MetaContext;
using UnityEngine;
using Virial.Media;
using Virial.Text;
using static Il2CppMono.Security.X509.X520;

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

    static public GUIContext GenerateContext(float scrollerHeight,float width)
    {
        var gui = NebulaImpl.Instance.GUILibrary;

        List<GUIContext> inner = new();
        var holder = new VerticalContextsHolder(Virial.Media.GUIAlignment.Left, inner);
        var attr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };
        var headerAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.1f) };
        var detailTitleAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) };
        var detailDetailAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.5f), Size = new(5f, 6f) };

        foreach (var a in NebulaAchievementManager.AllAchievements)
        {
            if (a.IsHidden) continue;

            if (inner.Count != 0) inner.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.08f)));

            var aContenxt = new HorizontalContextsHolder(GUIAlignment.Left,
                new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => Achievement.TrophySprite.GetSprite(a.Trophy)), new(0.38f, 0.38f), a.IsCleared ? Color.white : new UnityEngine.Color(0.2f, 0.2f, 0.2f)) { IsMasked = true },
                new NoSGUIMargin(GUIAlignment.Left, new(0.15f, 0.1f)),
                new VerticalContextsHolder(GUIAlignment.Center,
                    new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()),
                    new NoSGUIMargin(GUIAlignment.Left, new(0f, -0.021f)),
                    new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(Achievement.HiddenComponent)) { OverlayContext = a.GetOverlayContext() }
                )
                );
            inner.Add(aContenxt);
        }
        var scroller = new Nebula.Modules.MetaContext.GUIScrollView(GUIAlignment.Center, new(4.7f, scrollerHeight), holder);

        var cul = NebulaAchievementManager.Aggregate();
        List<GUIContext> footerList = new();
        for (int i = 0; i < cul.Length; i++)
        {
            int copiedIndex = i;
            if (footerList.Count != 0) footerList.Add(new NoSGUIMargin(GUIAlignment.Center, new(0.2f, 0f)));

            footerList.Add(new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => Achievement.TrophySprite.GetSprite(copiedIndex)), new(0.5f, 0.5f)));
            footerList.Add(new NoSGUIMargin(GUIAlignment.Center, new(0.05f, 0f)));
            footerList.Add(new NoSGUIText(GUIAlignment.Left, detailDetailAttr, new RawTextComponent(cul[i].num + "/" + cul[i].max)));
        }
        var footer = new HorizontalContextsHolder(GUIAlignment.Center, footerList.ToArray());

        return new VerticalContextsHolder(Virial.Media.GUIAlignment.Left, scroller, new NoSGUIMargin(GUIAlignment.Center, new(0f, 0.15f)), footer,
            new NoSGUIText(GUIAlignment.Center, detailDetailAttr, new RawTextComponent(Language.Translate("achievement.ui.allAchievements") + ": " + cul.Sum(c => c.num) + "/" + cul.Sum(c => c.max))))
        { FixedWidth = width };
    }

    public void OnShown() {
        var gui = NebulaImpl.Instance.GUILibrary;

        var title = new NoSGUIText(GUIAlignment.Left, gui.GetAttribute(Virial.Text.AttributeAsset.OblongHeader), new TranslateTextComponent("achievement.ui.title"));

        gameObject.SetActive(true);
        myScreen.SetContext(new Modules.MetaContext.VerticalContextsHolder(Virial.Media.GUIAlignment.Left, title, GenerateContext(4f, 9f)), out _);

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
    
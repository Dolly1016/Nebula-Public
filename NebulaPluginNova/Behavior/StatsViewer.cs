using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;
using Virial.Text;
using Virial;
using Virial.Game;
using Virial.Assignable;

namespace Nebula.Behavior;

internal class StatsViewer : MonoBehaviour
{
    static StatsViewer() => ClassInjector.RegisterTypeInIl2Cpp<StatsViewer>();
    private MetaScreen myScreen = null!;

    protected void Close() => MainMenuManagerInstance.Close(this);

    static public void Open(MainMenuManager mainMenu) => MainMenuManagerInstance.Open<StatsViewer>("StatsViewer", mainMenu, viewer => viewer.OnShown());
    

    static public GUIWidget GenerateWidget()
    {
        var gui = NebulaAPI.GUI;

        List<GUIWidget> inner = new();
        var holder = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, inner) { FixedWidth = 4.5f };

        var groupAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(2.2f) };
        var subGroupAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(1.8f) };
        var titleAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.6f) };
        var numAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardLeftNonFlexible)) { FontSize = new(1.6f), Size = new(0.4f, 0.2f), Alignment = Virial.Text.TextAlignment.Right };
        var semicolon = new NoSGUIText(GUIAlignment.Right, titleAttr, GUI.API.RawTextComponent(":"));
        var hyphen = new NoSGUIText(GUIAlignment.Right, subGroupAttr, GUI.API.RawTextComponent("-"));
        var hMargin = GUI.API.HorizontalMargin(0.05f);
        var tabMargin = GUI.API.HorizontalMargin(0.25f);
        var vMargin = GUI.API.HorizontalMargin(0.12f);

        GameStatsCategory lastCategory = (GameStatsCategory)(-1);
        DefinedAssignable? lastRelatedAssignable = null;
        foreach (var stats in NebulaAchievementManager.AllStats)
        {
            if (stats.Progress == 0) continue;

            if(stats.Category != lastCategory)
            {
                if (inner.Count != 0) inner.Add(vMargin);
                lastCategory = stats.Category;
                lastRelatedAssignable = null;
                inner.Add(GUI.API.LocalizedText(GUIAlignment.Left, groupAttr, "stats.group." + lastCategory.ToString().HeadLower()));
            }
            if (stats.RelatedAssignable != lastRelatedAssignable)
            {
                if(lastRelatedAssignable != null) inner.Add(vMargin);
                lastRelatedAssignable = stats.RelatedAssignable;
                if (lastRelatedAssignable != null) inner.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, tabMargin, hyphen, hMargin, GUI.API.RawText(GUIAlignment.Left, subGroupAttr, lastRelatedAssignable.DisplayColoredName)));
                else inner.Add(GUI.API.VerticalMargin(0.2f));
            }

            inner.Add(
                GUI.API.HorizontalHolder(GUIAlignment.Right,
                    tabMargin,
                    tabMargin,
                    new NoSGUIText(GUIAlignment.Right, titleAttr, stats.DisplayName),
                    hMargin,
                    semicolon,
                    hMargin,
                    new NoSGUIText(GUIAlignment.Right, numAttr, GUI.API.RawTextComponent(stats.Progress.ToString()))
                    ));
        }
        
        return new Nebula.Modules.GUIWidget.GUIScrollView(GUIAlignment.Center, new(5.4f, 4.2f), holder) { ScrollerTag = "statsViewer" };
    }

    public void OnShown()
    {
        var gui = NebulaAPI.GUI;

        var title = new NoSGUIText(GUIAlignment.Left, gui.GetAttribute(Virial.Text.AttributeAsset.OblongHeader), new TranslateTextComponent("stats.ui.title"));
        var caption = new NoSGUIText(GUIAlignment.Center, gui.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new TranslateTextComponent("stats.ui.caption").Italic().Color(Virial.Color.Gray));

        gameObject.SetActive(true);
        myScreen.SetWidget(new Modules.GUIWidget.VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, title, GenerateWidget(), GUI.API.VerticalMargin(0.15f), caption) { FixedWidth = 9f }, out _);

    }

    public void Awake()
    {
        myScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close());
        myScreen.SetBorder(new(9f, 5.5f));
    }
}

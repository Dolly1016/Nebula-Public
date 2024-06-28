using Virial;
using Virial.Text;
using Virial.Assignable;
using Virial.Media;
using Virial.Helpers;
using Nebula.Modules.GUIWidget;
using System;
using Nebula.Modules;
using Nebula.Utilities;

namespace DefaultLang.Documents;

static public class RoleDocumentHelper
{
    static private TextAttribute RoleNameAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(2.8f) };
    static private TextAttribute ChapterTitleAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(1.7f) };
    static public GUIWidget? GetAssignableNameWidget(DefinedAssignable? assignable)
    {
        var gui = NebulaAPI.GUI;
        if (assignable == null) return null;

        var title = gui.Text(Virial.Media.GUIAlignment.Left, RoleNameAttribute, gui.RawTextComponent(assignable?.DisplayColordName ?? "ERROR"));

        GUIWidget citationWidget = gui.EmptyWidget;
        if(assignable is HasCitation citation && citation.Citaion != null)
        {
            GUIClickableAction? onClick = citation.Citaion.RelatedUrl != null ? _ => UnityEngine.Application.OpenURL(citation.Citaion.RelatedUrl) : null;
            var overlay = (citation.Citaion.RelatedUrl != null) ? gui.LocalizedText(GUIAlignment.Left, gui.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.citation.openUrl") : null;

            if (citation?.Citaion.LogoImage != null) citationWidget = gui.Image(GUIAlignment.Bottom, citation.Citaion.LogoImage, new(1.5f, 0.37f), onClick, overlay);

            citationWidget = new NoSGUIText(GUIAlignment.Bottom, gui.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), citation!.Citaion.Name)
            {
                OverlayWidget = overlay,
                OnClickText = onClick != null ? (() => onClick?.Invoke(null!), false) : null
            };
        }

        return gui.VerticalHolder(GUIAlignment.TopLeft, gui.HorizontalHolder(GUIAlignment.Left, title, gui.HorizontalMargin(0.25f), citationWidget), gui.VerticalMargin(0.1f));
    }

    static private GUIWidget GetConfigurationsWidget(DefinedAssignable assignable)
    {
        if (assignable.ConfigurationHolder == null) return NebulaAPI.GUI.EmptyWidget;

        var text = string.Join("<br>", assignable.ConfigurationHolder.Configurations.Select(c => c.GetDisplayText()).Where(s => s != null));
        return NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), text);
    }
    static public GUIWidget GetConfigurationsChapter(DefinedAssignable assignable) => GetChapter("document.configurations", [GetConfigurationsWidget(assignable)]);

    static public GUIWidget GetRoleWidget(string internalName, params GUIWidget[] inner) => GetAssignableWidget(NebulaAPI.GetRole(internalName));
    static public GUIWidget GetModifierWidget(string internalName, params GUIWidget[] inner) => GetAssignableWidget(NebulaAPI.GetModifier(internalName));
    static public GUIWidget GetGhostRoleWidget(string internalName, params GUIWidget[] inner) => GetAssignableWidget(NebulaAPI.GetGhostRole(internalName));


    static private GUIWidget GetAssignableWidget(DefinedAssignable? assignable, params GUIWidget[] inner)
    {
        if(assignable == null)
            return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, [GetAssignableNameWidget(assignable), ..inner, GetAchievementWidget(assignable)]);
        else
            return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, [GetAssignableNameWidget(assignable), GetConfigurationsChapter(assignable), ..inner, GetAchievementWidget(assignable)] );
    }

    static private GUIWidget GetAchievementWidget(DefinedAssignable? assignable)
    {
        if (assignable == null) return NebulaAPI.GUI.EmptyWidget;

        var attr = new Virial.Text.TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };

        GUIWidget AchievementTitleWidget(Nebula.Modules.AbstractAchievement a) => new HorizontalWidgetsHolder(GUIAlignment.Left,
                NebulaAPI.GUI.Image(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new Virial.Compat.FuzzySize(0.38f, 0.38f)),
                NebulaAPI.GUI.Margin(new(0.15f,0.1f)),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(AbstractAchievement.HiddenComponent)) { OverlayWidget = a.GetOverlayWidget(true, false, true, false, a.IsCleared) }
                );

        var achievements = Nebula.Modules.NebulaAchievementManager.AllAchievements.Where(a => a.Category.role == assignable).Select(AchievementTitleWidget);

        return GetChapter("document.achievements", [NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, achievements)]);
    }
    static public GUIWidget GetChapter(string chapterName) => GetChapter(chapterName, [NebulaAPI.GUI.LocalizedText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), chapterName + ".main")]);
    static public GUIWidget GetChapter(string chapterName, Func<string,string> decorator) => GetChapter(chapterName, [GetDocumentText(decorator.Invoke(NebulaAPI.Language.Translate(chapterName + ".main")))]);
    static public GUIWidget GetChapter(string chapterName, GUIWidget?[] inner)
    {
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.TopLeft,
            NebulaAPI.GUI.LocalizedText(GUIAlignment.TopLeft, ChapterTitleAttribute, chapterName + ".title"),
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.TopLeft, NebulaAPI.GUI.HorizontalMargin(0.1f), NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, inner.Delimit(NebulaAPI.GUI.VerticalMargin(0.1f)))),
            NebulaAPI.GUI.VerticalMargin(0.1f)
            );
    }
    static public GUIWidget GetDocumentText(string rawText) => NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), rawText);
    static public GUIWidget GetDocumentLocalizedText(string translationKey) => NebulaAPI.GUI.LocalizedText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), translationKey);
    
    static public GUIWidget GetConfigurationCaption() => NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, NebulaAPI.GUI.VerticalMargin(0.2f), GetDocumentLocalizedText("document.caption.configuration"));
    
    static public GUIWidget GetImageContent(Image image, GUIWidget widget) => NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Left, NebulaAPI.GUI.Image(Virial.Media.GUIAlignment.Left, image, new(0.55f, null)), NebulaAPI.GUI.HorizontalMargin(0.15f), widget);
    static public GUIWidget GetImageContent(string nebulaImagePath, GUIWidget widget) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, widget);
    static public GUIWidget GetImageContent(string nebulaImagePath, string rawText) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, GetDocumentText(rawText));
    static public GUIWidget GetImageLocalizedContent(string nebulaImagePath, string translationKey) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, GetDocumentLocalizedText(translationKey));
}
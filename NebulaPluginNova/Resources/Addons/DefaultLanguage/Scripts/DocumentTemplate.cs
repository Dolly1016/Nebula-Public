using Virial;
using Virial.Text;
using Virial.Assignable;
using Virial.Media;
using Virial.Helpers;
using Nebula.Modules.GUIWidget;
using System;

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
    static public GUIWidget? GetRoleNameWidget(string internalName) => GetAssignableNameWidget(NebulaAPI.GetRole(internalName));
    
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
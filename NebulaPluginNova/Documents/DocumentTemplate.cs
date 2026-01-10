using Virial;
using Virial.Text;
using Virial.Assignable;
using Virial.Media;
using Virial.Helpers;
using Nebula.Modules.GUIWidget;
using System;
using Nebula.Modules;
using Nebula.Utilities;
using System.Linq;
using Nebula.Player;
using Nebula.Configuration;
using Nebula;
using Nebula.Roles;

namespace Nebula.Documents;

static public class RoleDocumentHelper
{
    static private TextAttribute RoleBlurbAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(1.3f) };
    static private TextAttribute RoleNameAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(2.8f) };
    static private TextAttribute ChapterTitleAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(1.7f) };
    static public GUIWidget? GetAssignableNameWidget(DefinedAssignable? assignable, IEnumerable<AssignableDocumentReplacement> replacements)
    {
        var gui = NebulaAPI.GUI;
        if (assignable == null) return null;

        var blurb = gui.Text(Virial.Media.GUIAlignment.Left, RoleBlurbAttribute, gui.RawTextComponent(assignable?.GeneralColoredBlurb ?? "ERROR"));
        var title = gui.Text(Virial.Media.GUIAlignment.Left, RoleNameAttribute, gui.RawTextComponent(assignable?.DisplayColoredName ?? "ERROR"));

        GUIWidget citationWidget = gui.EmptyWidget;
        if(assignable is HasCitation citation && citation.Citation != null)
        {
            GUIClickableAction? onClick = citation.Citation.RelatedUrl != null ? _ => UnityEngine.Application.OpenURL(citation.Citation.RelatedUrl) : null;
            var overlay = (citation.Citation.RelatedUrl != null) ? gui.LocalizedText(GUIAlignment.Left, gui.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.citation.openUrl") : null;

            if (citation?.Citation.LogoImage != null) citationWidget = gui.Image(GUIAlignment.Bottom, citation.Citation.LogoImage, new(1.5f, 0.37f), onClick, overlay);
            else
            {
                citationWidget = new NoSGUIText(GUIAlignment.Bottom, gui.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), citation!.Citation.Name)
                {
                    OverlayWidget = overlay,
                    OnClickText = onClick != null ? (() => onClick?.Invoke(null!), false) : null
                };
            }
        }

        var docText = assignable?.DocumentContent ?? "";
        foreach (var r in replacements) docText = docText.Replace(r.Key, r.Replacement);

        return gui.VerticalHolder(GUIAlignment.TopLeft,
                    gui.HorizontalHolder(GUIAlignment.Left,
                        gui.HorizontalMargin(-0.07f),
                        gui.VerticalHolder(GUIAlignment.Left, 
                            gui.VerticalMargin(0.05f),
                            gui.RoleIcon(GUIAlignment.Center, assignable, 0f, new(0.55f, 0.55f), 0.05f)
                        ),
                        gui.HorizontalMargin(0.02f),
                        gui.VerticalHolder(GUIAlignment.Left,
                            blurb,
                            gui.VerticalMargin(-0.06f),
                            title,
                            gui.VerticalMargin(-0.05f)
                        ),
                        gui.HorizontalMargin(0.25f),
                        citationWidget
                    ),
                    GetRoleFilterContent(assignable),
                    gui.VerticalMargin(0.15f),
                    GetDocumentText(docText),
                    gui.VerticalMargin(0.15f)
                );
    }

    static private GUIWidget GetConfigurationsWidget(DefinedAssignable assignable)
    {
        if (assignable.ConfigurationHolder == null) return NebulaAPI.GUI.EmptyWidget;

        var text = string.Join("\n", assignable.ConfigurationHolder.Configurations.Where(c => c.IsShown).Select(c => c.GetDisplayText()?.Replace("\n", "\n  ")).Where(s => s != null));
        if(text.Length == 0) text = Language.Translate("document.configurations.empty");
        return NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), text);
    }
    static public GUIWidget GetConfigurationsChapter(DefinedAssignable assignable) => GetChapter("document.configurations", [GetConfigurationsWidget(assignable)]);

    static internal GUIWidget GetAssignableWidget(DefinedAssignable? assignable, IEnumerable<AssignableDocumentReplacement> replacements, params GUIWidget?[] inner)
    {
        var notnullWidget = inner.Where(w => w != null);
        if (assignable == null)
            return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, [GetAssignableNameWidget(assignable, replacements), ..notnullWidget, GetAchievementWidget(assignable)]);
        else
            return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, [GetAssignableNameWidget(assignable, replacements), .. notnullWidget, GetConfigurationsChapter(assignable), GetAchievementWidget(assignable)]);
    }

    static public GUIWidget GetAchievementWidget(DefinedAssignable? assignable)
    {
        if (assignable == null) return NebulaAPI.GUI.EmptyWidget;

        var attr = new Virial.Text.TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };
        var headerAttr = new Virial.Text.TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.1f) };

        GUIWidget AchievementTitleWidget(Nebula.Modules.INebulaAchievement a) => new HorizontalWidgetsHolder(GUIAlignment.Left,
                NebulaAPI.GUI.Image(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new Virial.Compat.FuzzySize(0.38f, 0.38f)),
                NebulaAPI.GUI.Margin(new(0.15f,0.1f)),
                NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left,
                NebulaAPI.GUI.VerticalMargin(0.12f),
                new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()),
                NebulaAPI.GUI.VerticalMargin(-0.12f),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(INebulaAchievement.HiddenComponent)) { 
                    OverlayWidget = a.GetOverlayWidget(true, false, true, false, a.IsCleared),
                    OnClickText = a.IsCleared ? ((Action)(() =>
                    {
                        NebulaAchievementManager.SetOrToggleTitle(a);
                        VanillaAsset.PlaySelectSE();
                    }), true) : null
                }
                ));

        var achievements = Nebula.Modules.NebulaAchievementManager.AllAchievements.Where(a => assignable.AchievementGroups.Any(role => a.RelatedRole.Contains(role)) && !a.IsHidden).ToArray() ?? [];
        var widgets =
            achievements.Length == 0 ?
            [NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), Language.Translate("document.titles.none"))] :
            achievements.Where(a => a.RelatedRole.Count() <= 1).Concat(achievements.Where(a => a.RelatedRole.Count() > 1)).Select(AchievementTitleWidget);

        return GetChapter("document.titles", [NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, widgets)]);
    }
    static private string ReplaceVariableText(string orig) => orig
        .Replace("<var>", "<b><color=#00ffffff>")
        .Replace("</var>", "</color></b>")
        .Replace("<client>", "<b><color=#20ff20ff>")
        .Replace("</client>", "</color></b>")
        .Replace("<title>", "<b><size=110%>")
        .Replace("</title>", "</size></b><br>");

    static public GUIWidget GetChapter(string chapterName, IEnumerable<AssignableDocumentReplacement> replacements) => GetChapter(chapterName, [GetDocumentLocalizedText(chapterName + ".main", replacements)]);
    static public GUIWidget GetChapter(string chapterName, GUIWidget?[] inner)
    {
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.TopLeft,
            NebulaAPI.GUI.LocalizedText(GUIAlignment.TopLeft, ChapterTitleAttribute, chapterName + ".title"),
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.TopLeft, NebulaAPI.GUI.HorizontalMargin(0.1f), NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, inner.Where(w => w != null).Delimit(NebulaAPI.GUI.VerticalMargin(0.1f)))),
            NebulaAPI.GUI.VerticalMargin(0.1f)
            );
    }

    static public GUIWidget GetTipsChapter(string assignableName, IEnumerable<AssignableDocumentReplacement> replacements) => GetChapter("document.tips", [GetDocumentLocalizedText(assignableName + ".tips", replacements)]);
    static public GUIWidget GetWinCondChapter(string assignableName, IEnumerable<AssignableDocumentReplacement> replacements) => GetChapter("document.winCond", [GetDocumentLocalizedText(assignableName + ".winCond", replacements)]);
    static public GUIWidget GetDocumentText(string? rawText) => rawText == null ? NebulaAPI.GUI.EmptyWidget : NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(rawText));
    static public GUIWidget GetDocumentLocalizedText(string translationKey) => NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(NebulaAPI.Language.Translate(translationKey)));
    static public GUIWidget GetDocumentLocalizedText(string translationKey, IEnumerable<AssignableDocumentReplacement> replacements)
    {
        var text = NebulaAPI.Language.Translate(translationKey);
        foreach(var r in replacements) text = text.Replace(r.Key, r.Replacement);
        return NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(text));
    }

    static public GUIWidget GetConfigurationCaption() => NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, NebulaAPI.GUI.VerticalMargin(0.2f), GetDocumentLocalizedText("document.caption.configuration"));
    
    static public GUIWidget GetImageContent(Image image, GUIWidget widget) => NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Left, NebulaAPI.GUI.Image(Virial.Media.GUIAlignment.Left, image, new(0.55f, 0.65f)), NebulaAPI.GUI.HorizontalMargin(0.15f), widget);
    static public GUIWidget GetImageContent(string nebulaImagePath, GUIWidget widget) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, widget);
    static public GUIWidget GetImageContent(string nebulaImagePath, string rawText) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, GetDocumentText(rawText));
    static public GUIWidget GetImageLocalizedContent(Image image, string translationKey, IEnumerable<AssignableDocumentReplacement> replacements) => GetImageContent(image, GetDocumentLocalizedText(translationKey, replacements));

    static public string ConfigBool(string configurationId, string ifTrue, string ifFalse = "")
    {
        bool? val = NebulaAPI.Configurations.GetSharableVariable<bool>(configurationId)?.Value;
        if (!val.HasValue) return "ERROR";

        string str = val.Value ? ifTrue : ifFalse;
        return str.Length > 0 ? NebulaAPI.Language.Translate(str) : "";
    }

    static public string ConfigBoolRaw(string configurationId, string ifTrue, string ifFalse = "")
    {
        bool? val = NebulaAPI.Configurations.GetSharableVariable<bool>(configurationId)?.Value;
        if (!val.HasValue) return "ERROR";

        return val.Value ? ifTrue : ifFalse;
    }

    static public string Config<T>(string configurationId)
    {
        return NebulaAPI.Configurations.GetSharableVariable<T>(configurationId)?.Value?.ToString() ?? "ERROR";
    }

    static public GUIWidget GetRoleFilterContent(DefinedAssignable? assignable)
    {
        if (assignable == null || assignable is not HasRoleFilter)
        {
            return GUIEmptyWidget.Default;
        }
        bool canAssignToCrewmate = true, canAssignToImpostor = true, canAssignToNeutral = true;
        if(assignable is DefinedAllocatableModifierTemplate damt)
        {
            canAssignToCrewmate = damt.CanAssignOnThisGameByConfiguration(RoleCategory.CrewmateRole);
            canAssignToImpostor = damt.CanAssignOnThisGameByConfiguration(RoleCategory.ImpostorRole);
            canAssignToNeutral = damt.CanAssignOnThisGameByConfiguration(RoleCategory.NeutralRole);
        }
        
        return GetDocumentText(Language.Translate("document.canAssignTo") + ": " + RoleFilterHelper.GetFilterDisplayString(assignable, canAssignToCrewmate, canAssignToImpostor, canAssignToNeutral));
    }

    static private Image arrowImage = SpriteLoader.FromResource("Nebula.Resources.Documents.Arrow.png", 100f);
    static internal Image ArrowImage => arrowImage;

    static private Image minimapCrewImage = SpriteLoader.FromResource("Nebula.Resources.Documents.MinimapCrewmate.png", 100f);
    static internal Image MinimapCrewImage => minimapCrewImage;
}
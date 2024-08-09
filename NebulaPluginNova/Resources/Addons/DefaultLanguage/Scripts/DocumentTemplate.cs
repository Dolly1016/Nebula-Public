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

namespace DefaultLang.Documents;

static public class RoleDocumentHelper
{
    static private TextAttribute RoleBlurbAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(1.3f) };
    static private TextAttribute RoleNameAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(2.8f) };
    static private TextAttribute ChapterTitleAttribute = new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.DocumentBold)) { FontSize = new(1.7f) };
    static public GUIWidget? GetAssignableNameWidget(DefinedAssignable? assignable)
    {
        var gui = NebulaAPI.GUI;
        if (assignable == null) return null;

        var blurb = gui.Text(Virial.Media.GUIAlignment.Left, RoleBlurbAttribute, gui.RawTextComponent(assignable?.GeneralColoredBlurb ?? "ERROR"));
        var title = gui.Text(Virial.Media.GUIAlignment.Left, RoleNameAttribute, gui.RawTextComponent(assignable?.DisplayColoredName ?? "ERROR"));

        GUIWidget citationWidget = gui.EmptyWidget;
        if(assignable is HasCitation citation && citation.Citaion != null)
        {
            GUIClickableAction? onClick = citation.Citaion.RelatedUrl != null ? _ => UnityEngine.Application.OpenURL(citation.Citaion.RelatedUrl) : null;
            var overlay = (citation.Citaion.RelatedUrl != null) ? gui.LocalizedText(GUIAlignment.Left, gui.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.citation.openUrl") : null;

            if (citation?.Citaion.LogoImage != null) citationWidget = gui.Image(GUIAlignment.Bottom, citation.Citaion.LogoImage, new(1.5f, 0.37f), onClick, overlay);
            else
            {
                citationWidget = new NoSGUIText(GUIAlignment.Bottom, gui.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), citation!.Citaion.Name)
                {
                    OverlayWidget = overlay,
                    OnClickText = onClick != null ? (() => onClick?.Invoke(null!), false) : null
                };
            }
        }

        return gui.VerticalHolder(GUIAlignment.TopLeft, gui.HorizontalHolder(GUIAlignment.Left, gui.VerticalHolder(GUIAlignment.Left, blurb, gui.VerticalMargin(-0.06f), title, gui.VerticalMargin(-0.05f)), gui.HorizontalMargin(0.25f), citationWidget), GetRoleFilterContent(assignable), gui.VerticalMargin(0.15f), GetDocumentText(assignable?.ConfigurationHolder?.Detail.GetString()), gui.VerticalMargin(0.15f));
    }

    static private GUIWidget GetConfigurationsWidget(DefinedAssignable assignable)
    {
        if (assignable.ConfigurationHolder == null) return NebulaAPI.GUI.EmptyWidget;

        var text = string.Join("<br>", assignable.ConfigurationHolder.Configurations.Where(c => c.IsShown).Select(c => c.GetDisplayText()).Where(s => s != null));
        return NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), text);
    }
    static public GUIWidget GetConfigurationsChapter(DefinedAssignable assignable) => GetChapter("document.configurations", [GetConfigurationsWidget(assignable)]);

    static public GUIWidget GetRoleWidget(string internalName, params GUIWidget?[] inner) => GetAssignableWidget(NebulaAPI.GetRole(internalName), inner);
    static public GUIWidget GetModifierWidget(string internalName, params GUIWidget?[] inner) => GetAssignableWidget(NebulaAPI.GetModifier(internalName), inner);
    static public GUIWidget GetGhostRoleWidget(string internalName, params GUIWidget?[] inner) => GetAssignableWidget(NebulaAPI.GetGhostRole(internalName), inner);

    static public GUIWidget GetAssignableWidget(RoleType roleType, string internalName, params GUIWidget?[] inner) => roleType switch
    {
        RoleType.Role => GetRoleWidget(internalName, inner),
        RoleType.Modifier => GetModifierWidget(internalName, inner),
        RoleType.GhostRole => GetGhostRoleWidget(internalName, inner),
        _ => GUIEmptyWidget.Default
    };
    static private GUIWidget GetAssignableWidget(DefinedAssignable? assignable, params GUIWidget?[] inner)
    {
        var notnullWidget = inner.Where(w => w != null);
        if (assignable == null)
            return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, [GetAssignableNameWidget(assignable), ..notnullWidget, GetAchievementWidget(assignable)]);
        else
            return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, [GetAssignableNameWidget(assignable), GetConfigurationsChapter(assignable), ..notnullWidget, GetAchievementWidget(assignable)]);
    }

    static public GUIWidget GetAchievementWidget(DefinedAssignable? assignable)
    {
        if (assignable == null) return NebulaAPI.GUI.EmptyWidget;

        var attr = new Virial.Text.TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };
        var headerAttr = new Virial.Text.TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.1f) };

        GUIWidget AchievementTitleWidget(Nebula.Modules.AbstractAchievement a) => new HorizontalWidgetsHolder(GUIAlignment.Left,
                NebulaAPI.GUI.Image(GUIAlignment.Left, new WrapSpriteLoader(() => AbstractAchievement.TrophySprite.GetSprite(a.Trophy)), new Virial.Compat.FuzzySize(0.38f, 0.38f)),
                NebulaAPI.GUI.Margin(new(0.15f,0.1f)),
                NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left,
                NebulaAPI.GUI.VerticalMargin(0.12f),
                new NoSGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()),
                NebulaAPI.GUI.VerticalMargin(-0.12f),
                new NoSGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(AbstractAchievement.HiddenComponent)) { OverlayWidget = a.GetOverlayWidget(true, false, false, false, a.IsCleared) }
                ));

        var achievements = Nebula.Modules.NebulaAchievementManager.AllAchievements.Where(a => assignable.AchievementGroups.Any(role => role == a.Category.role) && !a.IsHidden).Select(AchievementTitleWidget);

        return GetChapter("document.titles", [NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, achievements)]);
    }
    static private string ReplaceVariableText(string orig) => orig.Replace("<var>", "<b><color=#00ffffff>").Replace("</var>", "</color></b>");
    static public GUIWidget GetChapter(string chapterName) => GetChapter(chapterName, [NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(NebulaAPI.Language.Translate(chapterName + ".main")))]);
    static public GUIWidget GetChapter(string chapterName, Func<string,string> decorator) => GetChapter(chapterName, [GetDocumentText(decorator.Invoke(NebulaAPI.Language.Translate(chapterName + ".main")))]);
    static public GUIWidget GetChapter(string chapterName, GUIWidget?[] inner)
    {
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.TopLeft,
            NebulaAPI.GUI.LocalizedText(GUIAlignment.TopLeft, ChapterTitleAttribute, chapterName + ".title"),
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.TopLeft, NebulaAPI.GUI.HorizontalMargin(0.1f), NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, inner.Where(w => w != null).Delimit(NebulaAPI.GUI.VerticalMargin(0.1f)))),
            NebulaAPI.GUI.VerticalMargin(0.1f)
            );
    }
    static public GUIWidget GetTipsChapter(string assignableName) => GetChapter("document.tips", [GetDocumentLocalizedText(assignableName + ".tips")]);
    static public GUIWidget GetTipsChapter(string assignableName, Func<string, string> decorator) => GetChapter("document.tips", [GetDocumentLocalizedText(assignableName + ".tips", decorator)]);
    static public GUIWidget GetWinCondChapter(string assignableName) => GetChapter("document.winCond", [GetDocumentLocalizedText(assignableName + ".winCond")]);
    static public GUIWidget GetWinCondChapter(string assignableName, Func<string, string> decorator) => GetChapter("document.winCond", [GetDocumentLocalizedText(assignableName + ".winCond", decorator)]);
    static public GUIWidget GetDocumentText(string? rawText) => rawText == null ? NebulaAPI.GUI.EmptyWidget : NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(rawText));
    static public GUIWidget GetDocumentLocalizedText(string translationKey) => NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(NebulaAPI.Language.Translate(translationKey)));
    static public GUIWidget GetDocumentLocalizedText(string translationKey, Func<string, string> decorator) => NebulaAPI.GUI.RawText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentStandard), ReplaceVariableText(decorator.Invoke(NebulaAPI.Language.Translate(translationKey))));

    static public GUIWidget GetConfigurationCaption() => NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, NebulaAPI.GUI.VerticalMargin(0.2f), GetDocumentLocalizedText("document.caption.configuration"));
    
    static public GUIWidget GetImageContent(Image image, GUIWidget widget) => NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Left, NebulaAPI.GUI.Image(Virial.Media.GUIAlignment.Left, image, new(0.55f, null)), NebulaAPI.GUI.HorizontalMargin(0.15f), widget);
    static public GUIWidget GetImageContent(string nebulaImagePath, GUIWidget widget) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, widget);
    static public GUIWidget GetImageContent(string nebulaImagePath, string rawText) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, GetDocumentText(rawText));
    static public GUIWidget GetImageLocalizedContent(string nebulaImagePath, string translationKey) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, GetDocumentLocalizedText(translationKey));
    static public GUIWidget GetImageLocalizedContent(string nebulaImagePath, string translationKey, Func<string, string> decorator) => GetImageContent(NebulaAPI.NebulaAsset.GetResource(nebulaImagePath)?.AsImage(100f)!, GetDocumentLocalizedText(translationKey, decorator));

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
}
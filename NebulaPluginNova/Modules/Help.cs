﻿using Nebula.Behaviour;

using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using Nebula.Roles;
using Nebula.Roles.Assignment;
using Nebula.Roles.Neutral;
using Nebula.Utilities;
using Steamworks;
using System.Text;
using UnityEngine.Rendering;
using Virial.Assignable;
using Virial.Game;
using Virial.Media;
using Virial.Text;
using static Nebula.Modules.MetaWidgetOld;

namespace Nebula.Modules;

public static class HelpScreen
{
    [Flags]
    public enum HelpTab
    {
        MyInfo = 0x01,
        Roles = 0x02,
        Options = 0x10,
        Slides = 0x20,
        Achievements = 0x40,
        Overview = 0x80,
    }

    public record HelpTabInfo(HelpTab Tab,string TranslateKey)
    {
        public MetaWidgetOld.Button GetButton(MetaScreen screen, HelpTab currentTab, HelpTab validTabs) => new(() => ShowScreen(screen, Tab, validTabs), TabButtonAttr) { Color = currentTab == Tab ? Color.white : Color.gray, TranslationKey = TranslateKey };
        
    }

    public static HelpTabInfo[] AllHelpTabInfo = {
        new(HelpTab.MyInfo, "help.tabs.myInfo"),
        new(HelpTab.Roles, "help.tabs.roles"),
        new(HelpTab.Overview, "help.tabs.overview"),
        new(HelpTab.Options, "help.tabs.options"),
        new(HelpTab.Slides, "help.tabs.slides"),
        new(HelpTab.Achievements, "help.tabs.achievements")
    };

    private static float HelpHeight = 4.1f;

    private static MetaScreen? lastHelpScreen = null;
    public static bool OpenedAnyHelpScreen => lastHelpScreen;
    public static MetaScreen? LastHelpScreen => lastHelpScreen;
    public static void TryOpenHelpScreen(HelpTab tab, bool canCloseEasily = false)
    {
        if (!lastHelpScreen) lastHelpScreen = OpenHelpScreen(tab, canCloseEasily);
    }
    public static void TryCloseHelpScreen()
    {
        if (lastHelpScreen) lastHelpScreen!.CloseScreen();
    }
    private static MetaScreen OpenHelpScreen(HelpTab tab, bool canCloseEasily = false)
    {
        var screen = MetaScreen.GenerateWindow(new(7.8f, HelpHeight + 0.6f), HudManager.Instance.transform, new Vector3(0, 0, 0), true, false);

        HelpTab validTabs = HelpTab.Roles | HelpTab.Overview | HelpTab.Options | HelpTab.Achievements;

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) validTabs |= HelpTab.MyInfo;
        
        if (AmongUsClient.Instance.AmHost && (NebulaGameManager.Instance?.LobbySlideManager.IsValid ?? false)) validTabs |= HelpTab.Slides;

        //開こうとしているタブが存在しない場合は、ロール一覧を開く
        if ((tab & validTabs) == (HelpTab)0) tab = PlayerControl.AllPlayerControls.Count > 5 ? HelpTab.Overview : HelpTab.Roles;

        ShowScreen(screen,tab,validTabs, canCloseEasily);

        return screen;
    }

    private static TextAttributeOld TabButtonAttr = new(TextAttributeOld.BoldAttr) { Size = new(1.15f, 0.26f) };
    private static IMetaWidgetOld GetTabsWidget(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        List<IMetaParallelPlacableOld> tabs = new();

        foreach (var info in AllHelpTabInfo) if ((validTabs & info.Tab) != 0) tabs.Add(info.GetButton(screen, tab, validTabs));

        return new CombinedWidgetOld(0.5f,tabs.ToArray());
    }
    private static void ShowScreen(MetaScreen screen, HelpTab tab,HelpTab validTabs, bool canCloseEasily = false)
    {
        MetaWidgetOld widget = new();
        Image? backImage = null;

        widget.Append(GetTabsWidget(screen, tab, validTabs));
        widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));

        switch (tab)
        {
            case HelpTab.MyInfo:
                widget.Append(ShowMyRolesSrceen(screen, out backImage));
                break;
            case HelpTab.Roles:
                widget.Append(ShowAssignableScreen());
                break;
            case HelpTab.Overview:
                widget.Append(ShowPreviewSrceen(out backImage));
                break;
            case HelpTab.Options:
                widget.Append(ShowOptionsScreen());
                break;
            case HelpTab.Slides:
                widget.Append(ShowSlidesScreen());
                break;
            case HelpTab.Achievements:
                widget.Append(ShowAchievementsScreen(canCloseEasily));
                break;
        }

        screen.SetWidget(widget);
        screen.SetBackImage(backImage, 0.09f);
    }

    static private void ShowDocumentScreen(IDocument doc, Image? illustlation)
    {
        var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, true);

        Virial.Compat.Artifact<GUIScreen>? inner = null;
        var scrollView = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 4.5f), () => doc.Build(inner) ?? GUIEmptyWidget.Default);
        inner = scrollView.Artifact;
        Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();

        screen.SetWidget(scrollView, illustlation, out _);
    }

    private static TextAttributeOld RoleTitleAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static TextAttributeOld RoleTitleAttrUnmasked = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f) };
    
    private static Virial.Media.GUIWidget GetAssignableOverlay(DefinedAssignable assignable)
    {
        List<Virial.Media.GUIWidget> widgets = new();

        widgets.Add(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new RawTextComponent(assignable.DisplayColoredName)));
        widgets.Add(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), assignable?.ConfigurationHolder?.Detail));
        if (assignable is HasCitation hc && hc.Citaion != null)
        {
            var citation = hc.Citaion;

            widgets.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.35f)));
            widgets.Add(new HorizontalWidgetsHolder(GUIAlignment.Left,
                new NoSGUIText(GUIAlignment.Bottom, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new RawTextComponent("from")),
                new NoSGUIMargin(GUIAlignment.Bottom, new(0.12f, 0f)),
                citation!.LogoImage != null ? GUI.Instance.Image(GUIAlignment.Bottom, citation.LogoImage, new(1.5f, 0.37f)) : new NoSGUIText(GUIAlignment.Left, GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), citation!.Name)));
        }

        return new VerticalWidgetsHolder(GUIAlignment.Left, widgets) { BackImage = assignable?.ConfigurationHolder?.Illustration };
    }
    static private void OpenAssignableHelp(DefinedAssignable assignable)
    {
        var doc = DocumentManager.GetDocument("role." + assignable.InternalName);
        if (doc == null)
        {
            Debug.Log("Not Existed: " + "role." + assignable.InternalName);
            return;
        }
        ShowDocumentScreen(doc, assignable.ConfigurationHolder?.Illustration);
    }

    private static IMetaWidgetOld ShowAssignableScreen()
    {
        (IEnumerable<DefinedAssignable> roles, TextComponent label)[] assignables = [
            (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.ImpostorRole && (r as DefinedAssignable).ShowOnHelpScreen), GUI.API.TextComponent(new(Palette.ImpostorRed), "role.category.impostor")),
            (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.NeutralRole && (r as DefinedAssignable).ShowOnHelpScreen), GUI.API.TextComponent(new(1f, 0.7f, 0f), "role.category.neutral")),
            (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.CrewmateRole && (r as DefinedAssignable).ShowOnHelpScreen), GUI.API.TextComponent(new(Palette.CrewmateBlue), "role.category.crewmate")),
            (Roles.Roles.AllGhostRoles.Where(r => (r as DefinedAssignable).ShowOnHelpScreen),  GUI.API.TextComponent(new(Color.gray), "role.category.ghost")),
            (Roles.Roles.AllModifiers.Where(r => (r as DefinedAssignable).ShowOnHelpScreen), GUI.API.TextComponent(new(Palette.White), "role.category.modifier")),
            ];
        (IEnumerable<PerkFunctionalDefinition> perks, TextComponent label)[] perks = [
            (Roles.Roles.AllPerks.Where(p => p.PerkCategory == PerkFunctionalDefinition.Category.Standard), GUI.API.TextComponent(new(Color.white), "game.metaAbility.perks.standard")),
            (Roles.Roles.AllPerks.Where(p => p.PerkCategory == PerkFunctionalDefinition.Category.NoncrewmateOnly), GUI.API.TextComponent(new(Color.white), "game.metaAbility.perks.noncrewmateOnly")),
            ];

        MetaWidgetOld inner = new();

        void AddContent(TextComponent label, Action routine)
        {
            if (inner.Count > 0) inner.Append(new MetaWidgetOld.VerticalMargin(0.2f));
            inner.Append(new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), label)));
            inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));
            routine.Invoke();
        }

        foreach (var content in assignables)
        {
            AddContent(content.label, () =>
            {
                inner.Append(content.roles, (role) => new MetaWidgetOld.Button(() =>
                {
                    OpenAssignableHelp(role);
                }, RoleTitleAttr)
                {
                    RawText = role.DisplayColoredName,
                    PostBuilder = (PassiveButton button, SpriteRenderer renderer, TMPro.TextMeshPro text) =>
                    {
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        button.OnMouseOver.AddListener(() =>
                        {
                            NebulaManager.Instance.SetHelpWidget(button, GetAssignableOverlay(role));
                        });
                        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                    },
                    Alignment = IMetaWidgetOld.AlignmentOption.Center
                }, 4, -1, 0, 0.6f);
            });
        }

        foreach (var content in perks)
        {
            AddContent(content.label, () =>
            {
                inner.Append(content.perks, (perk) => new WrappedWidget(perk.PerkDefinition.GetPerkImageWidget(true, overlay: ()=>perk.PerkDefinition.GetPerkWidget())) , 8, -1, 0, 0.7f);
            });
        }

        return new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
    }


    private static TextAttributeOld SlideTitleAttr = new(TextAttributeOld.NormalAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(3.6f, 0.28f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static TextAttributeOld SlideButtonAttr = new(TextAttributeOld.BoldAttr) { Size = new(0.8f, 0.25f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static IMetaWidgetOld ShowSlidesScreen()
    {
        MetaWidgetOld inner = new();

        foreach (var temp in LobbySlideManager.AllTemplates)
        {
            var copiedTemp = temp;
            inner.Append(new CombinedWidgetOld(
                0.5f,
                new MetaWidgetOld.Text(SlideTitleAttr) { RawText = temp.Title },
                new MetaWidgetOld.HorizonalMargin(0.2f),
                new MetaWidgetOld.Button(()=> NebulaGameManager.Instance?.LobbySlideManager.TryRegisterAndShow(copiedTemp?.Generate()), SlideButtonAttr) { TranslationKey = "help.slides.share", PostBuilder = (_,renderer,_)=>renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask }
                ));
        }

        return new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
    }

    private static TextAttributeOld OptionsAttr = new(TextAttributeOld.BoldAttr) { FontSize = 1.6f, FontMaxSize = 1.6f, FontMinSize = 1.6f, Size = new(4f, 10f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Alignment = TMPro.TextAlignmentOptions.TopLeft };
    private static Reference<ScrollView.InnerScreen>? optionsInner = new();
    private static IMetaWidgetOld ShowOptionsScreen()
    {
        var view = new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight - 0.5f), GetOptionsWidget()) { Alignment = IMetaWidgetOld.AlignmentOption.Center, ScrollerTag = "HelpOptions", InnerRef = optionsInner };
        List<Virial.Media.GUIWidget> buttons = new();
        var textAttr = new TextAttribute(GUI.API.GetAttribute(AttributeAsset.OptionsButtonMedium)) { Font = GUI.API.GetFont(FontAsset.Gothic), Size = new(1.7f, 0.22f) };
        buttons.Add(GUI.API.LocalizedButton(GUIAlignment.Center, textAttr, "options.map.customization", (_) => GeneralConfigurations.OpenMapEditor(null, null, false)));
        if (GeneralConfigurations.SpawnMethodOption.GetValue() != 0) buttons.Add(GUI.API.LocalizedButton(GUIAlignment.Center, textAttr, "options.map.spawnCandidatesFilter", (_) => GeneralConfigurations.OpenCandidatesFilter(null, null, false)));
        var buttonsOld = new CombinedWidgetOld(0.5f, buttons.Select(b => new MetaWidgetOld.WrappedWidget(b)).ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
        return new MetaWidgetOld(view, new MetaWidgetOld.VerticalMargin(0.05f) , buttonsOld);
    }

    static private IMetaWidgetOld GetOptionsWidget()
    {
        StringBuilder builder = new();
        foreach (var holder in ConfigurationHolder.AllHolders.Where(h => h.DisplayOption != Virial.Configuration.ConfigurationHolderState.Inactivated))
        {
            if (!holder.IsShown || !holder.GameModes.Test(GeneralConfigurations.CurrentGameMode)) continue;

            if (builder.Length != 0) builder.Append("\n\n");
            builder.Append(holder.Title.GetString() + "\n   ");
            builder.Append(holder.Configurations.Where(c => c.IsShown).Select(c => c.GetDisplayText()?.Replace("\n", "\n   ")).Where(str => str != null).Join(null, "\n   "));
        }

        return new MetaWidgetOld.VariableText(OptionsAttr) { RawText = builder.ToString(), Alignment = IMetaWidgetOld.AlignmentOption.Center };
    }
    internal static void OnUpdateOptions()
    {
        if (!(optionsInner?.Value?.IsValid ?? false)) return;

        optionsInner.Value.SetWidget(GetOptionsWidget());
    }

    private static IMetaWidgetOld ShowMyRolesSrceen(MetaScreen outsideScreen, out Image? backImage)
    {
        MetaWidgetOld widget = new();

        Virial.Compat.Artifact<GUIScreen> inner = null!;

        widget.Append(GamePlayer.LocalPlayer!.AllAssigned().Where(a => a.CanBeAwareAssignment).Select(a => a.AssignableOnHelp).Smooth(),
            (role) => new MetaWidgetOld.Button(() =>
            {
                var doc = DocumentManager.GetDocument("role." + role.InternalName);
                if (doc == null) return;

                inner.Do(screen =>
                {
                    screen.SetWidget(doc.Build(inner), out _);
                    outsideScreen.ClearBackImage();
                    outsideScreen.SetBackImage(role.ConfigurationHolder?.Illustration, 0.09f);
                });
            }, RoleTitleAttrUnmasked)
            {
                RawText = role.DisplayName.Color(role.UnityColor),
                Alignment = IMetaWidgetOld.AlignmentOption.Center
            }, 128, -1, 0, 0.6f);

        var assignable = GamePlayer.LocalPlayer!.Role.AssignableOnHelp.First();
        var scrollView = new GUIScrollView(GUIAlignment.Left, new(7.4f, HelpHeight - 0.7f), () =>
        {
            var doc = DocumentManager.GetDocument("role." + assignable.InternalName);
            return doc?.Build(inner) ?? GUIEmptyWidget.Default;
        });
        inner = scrollView.Artifact;

        widget.Append(new MetaWidgetOld.WrappedWidget(scrollView));

        backImage = assignable.ConfigurationHolder?.Illustration;

        return widget;
    }

    private static IMetaWidgetOld ShowAchievementsScreen(bool closeOnChoosing)
    {
        Virial.Media.GUIWidget GenerateWidget(string? scrollerTag = null, Predicate<INebulaAchievement>? predicate = null, string? shownText = null) => AchievementViewer.GenerateWidget(3.15f, 6.2f, scrollerTag, true, predicate, shownText, onClicked: closeOnChoosing ? _ => TryCloseHelpScreen() : null);
        Virial.Compat.Artifact<GUIScreen> artifact = null!;
        List<Virial.Media.GUIWidget> buttons = new([
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.all")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget(), out var _))},
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.achieved")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementAchieved", a => a.IsCleared, Language.Translate("achievement.filter.achieved")), out var _))},
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.nonAchieved")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementDontAchieved", a => !a.IsCleared, Language.Translate("achievement.filter.nonAchieved")), out var _))},
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.search")){ OnClick = _ => {
                var searchWindow = MetaScreen.GenerateWindow(new(6f,0.8f), HudManager.Instance.transform, Vector3.zero, true, true, true);

                void ShowResult(string rawKeyword){
                    string[] keyword = rawKeyword.Split(' ','　').Where(s => s.Length >= 1).ToArray();
                    searchWindow.CloseScreen();

                    artifact.Do(screen => screen.SetWidget(GenerateWidget(predicate: ac => keyword.All(k => ac.GetKeywords().Any(acK => acK.Contains(k))), shownText: Language.Translate("achievement.ui.shown.search").Replace("%KEYWORD%", rawKeyword)), out var _));
                }

                var textField = new GUITextField(GUIAlignment.Left, new(4.3f,0.4f)){ HintText = Language.Translate("ui.dialog.keyword").Color(Color.gray), IsSharpField = false, WithMaskMaterial = true, EnterAction = (rawKeyword) => {ShowResult(rawKeyword); return true; } };
                var button = new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("ui.dialog.search")){OnClick = _ =>ShowResult(textField.Artifact.FirstOrDefault()?.Text ?? "")};
                searchWindow.SetWidget(GUI.API.HorizontalHolder(GUIAlignment.Center, textField, button), new Vector2(0.5f,0.5f), out var size);
                textField.Artifact.Do(field => field.GainFocus());
            } },
            ]);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized)
        {
            buttons.Add(new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.myRole")) { OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementMyRole", a => GamePlayer.LocalPlayer.AllAssigned().Any(r => r.Assignable == a.RelatedRole), Language.Translate("achievement.filter.myRole")), out var _)) });
        }

        var sidebar = new VerticalWidgetsHolder(GUIAlignment.Top, buttons);
        var screen = new GUIFixedView(GUIAlignment.Top, new(5.7f, 3.8f), GenerateWidget()) { WithMask = false };
        artifact = screen.Artifact;
        return new MetaWidgetOld.WrappedWidget(new HorizontalWidgetsHolder(GUIAlignment.Center, screen, sidebar));
    }

    private static IDividedSpriteLoader RolePreviewIconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.RolePreviewIcon.png", 100f, 10, 1);
    private static Dictionary<AssignmentPreview.AssignmentFlag, int> RolePreviewIconMap = new Dictionary<AssignmentPreview.AssignmentFlag, int>()
    {
        { AssignmentPreview.AssignmentFlag.VanillaImpostor, 0},
        { AssignmentPreview.AssignmentFlag.ModImpostor, 1},
        { AssignmentPreview.AssignmentFlag.VanillaImpostor | AssignmentPreview.AssignmentFlag.ModImpostor, 2},
        { AssignmentPreview.AssignmentFlag.ModNeutral, 3},
        { AssignmentPreview.AssignmentFlag.ModCrewmate | AssignmentPreview.AssignmentFlag.ModNeutral, 4},
        { AssignmentPreview.AssignmentFlag.VanillaCrewmate | AssignmentPreview.AssignmentFlag.ModNeutral, 5},
        { AssignmentPreview.AssignmentFlag.VanillaCrewmate | AssignmentPreview.AssignmentFlag.ModCrewmate | AssignmentPreview.AssignmentFlag.ModNeutral, 6},
        { AssignmentPreview.AssignmentFlag.ModCrewmate, 7},
        { AssignmentPreview.AssignmentFlag.VanillaCrewmate | AssignmentPreview.AssignmentFlag.ModCrewmate, 8},
        { AssignmentPreview.AssignmentFlag.VanillaCrewmate, 9}
    };

    private static NebulaSpriteLoader previewSpriteRaiderAndSniper = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Combinations/SniperRaider.png");
    private static IMetaWidgetOld ShowPreviewSrceen(out Image? backImage)
    {
        backImage = null;

        var textAttr = GUI.API.GetAttribute(AttributeAsset.OverlayContent);
        var maskedAttr = GUI.API.GetAttribute(AttributeAsset.DocumentStandard);
        var maskedTitleAttr = GUI.API.GetAttribute(AttributeAsset.DocumentTitle);
        var maskedSubtitleAttr = GUI.API.GetAttribute(AttributeAsset.DocumentSubtitle1);
        Virial.Media.GUIWidget GetAssignableText(DefinedAssignable assignable, string? displayName = null) => new NoSGUIText(GUIAlignment.Center, maskedAttr, new RawTextComponent(displayName ?? assignable.DisplayColoredName))
        {
            OverlayWidget = () => GetAssignableOverlay(assignable),
            OnClickText = (() => OpenAssignableHelp(assignable), false)
        };

        Virial.Media.GUIWidget GetRoleOverview(RoleCategory category, string categoryName, bool withJackalizedView = false)
        {
            List<Virial.Media.GUIWidget> list100 = new();
            List<Virial.Media.GUIWidget> listRandom = new();
            List<Virial.Media.GUIWidget> ghosts = new();
            List<Virial.Media.GUIWidget> modifiers = new();

            void CheckRoles(List<Virial.Media.GUIWidget> list100, List<Virial.Media.GUIWidget> listRandom, bool jackalAssignment = false)
            {
                foreach (var role in Roles.Roles.AllRoles.Where(r => jackalAssignment || r.Category == category))
                {
                    var param = jackalAssignment ? role.JackalAllocationParameters : role.AllocationParameters;
                    if ((param?.RoleCount100 ?? 0) > 0)
                    {
                        string numText = "x" + param!.RoleCount100;
                        if (param.RoleCountRandom > 0) numText += $" (+{param.RoleCountRandom}, {param.GetRoleChance(param!.RoleCount100 + 1)}%)";
                        list100.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role, jackalAssignment ? role.DisplayName.Color(Jackal.MyRole.UnityColor) : null), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                    }
                    else if ((param?.RoleCountRandom ?? 0) > 0)
                    {
                        string numText = $"x{param!.RoleCountRandom} ({param.GetRoleChance(1)}%)";
                        listRandom.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role, jackalAssignment ? role.DisplayName.Color(Jackal.MyRole.UnityColor) : null), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                    }
                }
            }
            CheckRoles(list100, listRandom);

            foreach (var m in Roles.Roles.AllAllocatableModifiers())
            {
                m.GetAssignProperties(category, out var assign100, out var assignRandom, out var assignChance);
                if (assign100 > 0 || assignRandom > 0)
                {
                    string numText = "x" + assign100.ToString();
                    if (assignRandom > 0) numText += $" (+{assignRandom}, {assignChance}%)";
                    modifiers.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(m), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                }
            }

            foreach (var g in Roles.Roles.AllGhostRoles)
            {
                g.GetAssignProperties(category, out var assign100, out var assignRandom, out var assignChance);
                if (assign100 > 0 || assignRandom > 0)
                {
                    string numText = "x" + assign100.ToString();
                    if (assignRandom > 0) numText += $" (+{assignRandom}, {assignChance}%)";
                    ghosts.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(g), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                }
            }

            List<Virial.Media.GUIWidget> result = [GUI.API.HorizontalMargin(2.2f), GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentTitle), Language.Translate("help.rolePreview.category." + categoryName).Bold())];
            if (list100.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.100") + "-").Bold()), .. list100, GUI.API.Margin(new(2f, 0.3f))]));
            if (listRandom.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.random") + "-").Bold()), .. listRandom, GUI.API.Margin(new(2f, 0.3f))]));
            if (modifiers.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.modifiers") + "-").Bold()), .. modifiers, GUI.API.Margin(new(2f, 0.3f))]));
            if (ghosts.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.ghostRoles") + "-").Bold()), .. ghosts, GUI.API.Margin(new(2f, 0.3f))]));
            if (withJackalizedView)
            {
                List<Virial.Media.GUIWidget> listJ100 = new();
                List<Virial.Media.GUIWidget> listJRandom = new();
                CheckRoles(listJ100, listJRandom, true);

                if (listJ100.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.jackalized.100") + "-").Bold()), .. listJ100, GUI.API.Margin(new(2f, 0.3f))]));
                if (listJRandom.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.jackalized.random") + "-").Bold()), .. listJRandom, GUI.API.Margin(new(2f, 0.3f))]));
            }

            return GUI.API.VerticalHolder(GUIAlignment.Top, result);
        }


        int players = PlayerControl.AllPlayerControls.Count;
        var flags = AssignmentPreview.CalcPreview(players);
        var iconHolder = GUI.API.HorizontalHolder(GUIAlignment.Center, flags.Select(f => new NoSGUIImage(
            GUIAlignment.Center, RolePreviewIconMap.TryGetValue(f, out var i) ? RolePreviewIconSprite.AsLoader(i) : null!, new(null,flags.Length >= 18 ? 0.33f : 0.48f), overlay: () =>
            {
                string text = Language.Translate("help.rolePreview.header").Bold() + "<br>";
                if ((f & AssignmentPreview.AssignmentFlag.ModImpostor) != 0) text += "<br>" + Language.Translate("help.rolePreview.modImpostor").Color(Palette.ImpostorRed);
                if ((f & AssignmentPreview.AssignmentFlag.VanillaImpostor) != 0) text += "<br>" + Language.Translate("help.rolePreview.vanillaImpostor").Color(Palette.ImpostorRed);
                if ((f & AssignmentPreview.AssignmentFlag.ModNeutral) != 0) text += "<br>" + Language.Translate("help.rolePreview.modNeutral").Color(Color.yellow);
                if ((f & AssignmentPreview.AssignmentFlag.ModCrewmate) != 0) text += "<br>" + Language.Translate("help.rolePreview.modCrewmate").Color(Palette.CrewmateBlue);
                if ((f & AssignmentPreview.AssignmentFlag.VanillaCrewmate) != 0) text += "<br>" + Language.Translate("help.rolePreview.vanillaCrewmate").Color(Palette.CrewmateBlue);

                return GUI.API.RawText(GUIAlignment.Center, textAttr, text);
            })
            ));

        List<Virial.Media.GUIWidget> winConds = [
            GUI.API.RawText(GUIAlignment.Left, maskedTitleAttr,"勝利条件"),
            ];
        GameEnd? end = null;
        foreach(var tip in DocumentTipManager.WinConditionTips)
        {
            if (end != tip.End)
            {
                winConds.Add(GUI.API.VerticalMargin(0.1f));
                winConds.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.2f), GUI.API.RawText(GUIAlignment.Left, maskedSubtitleAttr, tip.End.DisplayText.Replace("%EXTRA%", "").Color(tip.End.Color))));
                end = tip.End;
            }
            winConds.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.3f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, ("・" + tip.Title).Bold())));
            winConds.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.45f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, tip.Text)));
            winConds.Add(GUI.API.VerticalMargin(0.15f));
        }

        var view = new GUIScrollView(GUIAlignment.Center, new(7.4f, HelpHeight - 0.68f),
            GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.35f),
            GUI.API.VerticalHolder(GUIAlignment.Center,
                GUI.API.HorizontalHolder(GUIAlignment.Center, 
                GetRoleOverview(RoleCategory.ImpostorRole,"impostor"), 
                GeneralConfigurations.NeutralSpawnable ? GetRoleOverview(RoleCategory.NeutralRole, "neutral", (Jackal.MyRole as DefinedRole).IsSpawnable && Jackal.JackalizedImpostorOption) : GUI.API.EmptyWidget, 
                GetRoleOverview(RoleCategory.CrewmateRole, "crewmate")),
                GUI.API.VerticalHolder(GUIAlignment.Left, winConds)
            )));

        //背景画像の選定
        bool CheckSpawnable(params ISpawnable[] spawnables)
        {
            return spawnables.All(s => s.IsSpawnable && (s is not DefinedRole dr || GeneralConfigurations.exclusiveAssignmentOptions.All(option => !option.OnAssigned(dr).Any(r => spawnables.Contains(r)))));
        }

        if (CheckSpawnable(Roles.Impostor.Sniper.MyRole, Roles.Impostor.Raider.MyRole))
            backImage = previewSpriteRaiderAndSniper;

        return new MetaWidgetOld.WrappedWidget(new VerticalWidgetsHolder(GUIAlignment.Center, iconHolder, new NoSGUIMargin(GUIAlignment.Center, new(0f, 0.2f)), view));
    }
}

public class HintManager
{ 
    internal static List<Virial.Media.Hint> AllHints = new();

    static private Hint WithImage(string id) => new HintWithImage(SpriteLoader.FromResource("Nebula.Resources.Hints." + id.HeadUpper() + ".png", 100f), new TranslateTextComponent("hint." + id.HeadLower() + ".title"), new TranslateTextComponent("hint." + id.HeadLower() + ".detail"));
    static HintManager() {
        RegisterHint(WithImage("ProcessorAffinity"));
        RegisterHint(WithImage("GameConfigurationMenu"));
        RegisterHint(WithImage("HelpInGame"));
        RegisterHint(WithImage("NearClick"));
        RegisterHint(WithImage("Paparazzo"));
        RegisterHint(WithImage("Busker"));
        RegisterHint(WithImage("Spectator"));
        RegisterHint(WithImage("Freeplay"));
        RegisterHint(WithImage("KeyAssignment"));
        RegisterHint(WithImage("Screenshot"));
        RegisterHint(WithImage("Overlay"));
        RegisterHint(WithImage("Achievement"));
    }

    public static void RegisterHint(Virial.Media.Hint hint) => AllHints.Add(hint);

    public static IEnumerator CoShowHint(float delay = 0.5f)
    {
        yield return Effects.Wait(delay);

        var overlay = GameObject.Instantiate(TransitionFade.Instance.overlay, null);
        overlay.transform.position = TransitionFade.Instance.overlay.transform.position + new Vector3(0, 0, -100f);
        overlay.color = Color.black;
        overlay.gameObject.layer = LayerExpansion.GetUILayer();
        overlay.gameObject.AddComponent<SortingGroup>().sortingOrder = 150;

        var holder = UnityHelper.CreateObject("Holder", null, Vector3.zero);
        holder.transform.SetParent(overlay.transform);
        var anim = GameObject.Instantiate(AccountManager.Instance.waitingText.transform.GetChild(2).gameObject, holder.transform);
        AmongUsUtil.SetPlayerMaterial(anim.GetComponent<Renderer>(), DynamicPalette.MyColor.MainColor, DynamicPalette.MyColor.ShadowColor, DynamicPalette.MyVisorColor.ToColor(Palette.VisorColor));
        var scale = holder.transform.localScale;
        scale.x = -scale.x;
        holder.transform.localScale = scale;

        var mask = UnityHelper.CreateObject<SpriteMask>("Mask", overlay.transform, new Vector3(0, 0, 5f));
        mask.sprite = overlay.sprite;
        mask.transform.localScale = overlay.size;
        
        var hint = AllHints[System.Random.Shared.Next(AllHints.Count)].GUI.Invoke().Instantiate(new(new(0.5f, 0.5f), new(0f, 0f, 0f)), new(6f, 4f), out _);
        hint?.transform.SetParent(overlay.transform);
        if(hint) hint!.transform.localPosition = new(0f, 0f, 10f);

        yield return Effects.ColorFade(overlay, Color.black, Color.clear, 0.5f);

        yield return Effects.Wait(8f);

        var text = GUI.API.LocalizedText(GUIAlignment.TopRight, GUI.API.GetAttribute(AttributeAsset.OverlayContent), "hint.ui.exit").Instantiate(new Anchor(new(1f,1f), new(AmongUsUtil.GetCorner(1f,1f).AsVector3(-500f))), new(10f,10f), out _);
        text?.transform.SetParent(overlay.transform);
    }

}


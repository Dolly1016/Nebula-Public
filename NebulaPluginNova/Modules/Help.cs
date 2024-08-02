using System.Text;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Virial.Media;
using Nebula.Roles;
using Virial.Assignable;
using static Nebula.Modules.MetaWidgetOld;
using Virial.Text;
using Nebula.Modules.MetaWidget;
using UnityEngine.Rendering;
using Nebula.Roles.Assignment;
using NAudio.CoreAudioApi;

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
    public static void TryOpenHelpScreen(HelpTab tab)
    {
        if (!lastHelpScreen) lastHelpScreen = OpenHelpScreen(tab);
    }

    private static MetaScreen OpenHelpScreen(HelpTab tab)
    {
        var screen = MetaScreen.GenerateWindow(new(7.8f, HelpHeight + 0.6f), HudManager.Instance.transform, new Vector3(0, 0, 0), true, false);

        HelpTab validTabs = HelpTab.Roles | HelpTab.Overview | HelpTab.Options | HelpTab.Achievements;

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) validTabs |= HelpTab.MyInfo;
        
        if (AmongUsClient.Instance.AmHost && (NebulaGameManager.Instance?.LobbySlideManager.IsValid ?? false)) validTabs |= HelpTab.Slides;

        //開こうとしているタブが存在しない場合は、ロール一覧を開く
        if ((tab & validTabs) == (HelpTab)0) tab = PlayerControl.AllPlayerControls.Count > 5 ? HelpTab.Overview : HelpTab.Roles;

        ShowScreen(screen,tab,validTabs);

        return screen;
    }

    private static TextAttributeOld TabButtonAttr = new(TextAttributeOld.BoldAttr) { Size = new(1.15f, 0.26f) };
    private static IMetaWidgetOld GetTabsWidget(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        List<IMetaParallelPlacableOld> tabs = new();

        foreach (var info in AllHelpTabInfo) if ((validTabs & info.Tab) != 0) tabs.Add(info.GetButton(screen, tab, validTabs));

        return new CombinedWidgetOld(0.5f,tabs.ToArray());
    }
    private static void ShowScreen(MetaScreen screen, HelpTab tab,HelpTab validTabs)
    {
        MetaWidgetOld widget = new();

        widget.Append(GetTabsWidget(screen, tab, validTabs));
        widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));

        switch (tab)
        {
            case HelpTab.MyInfo:
                widget.Append(ShowMyRolesSrceen());
                break;
            case HelpTab.Roles:
                widget.Append(ShowAssignableScreen(
                    (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.ImpostorRole && (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), GUI.API.TextComponent(new(Palette.ImpostorRed), "role.category.impostor")))),
                    (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.NeutralRole && (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), GUI.API.TextComponent(new(1f,0.7f,0f), "role.category.neutral")))),
                    (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.CrewmateRole && (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), GUI.API.TextComponent(new(Palette.CrewmateBlue), "role.category.crewmate")))),
                    (Roles.Roles.AllGhostRoles.Where(r => (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), GUI.API.TextComponent(new(Color.gray), "role.category.ghost")))),
                    (Roles.Roles.AllModifiers.Where(r => (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), GUI.API.TextComponent(new(Palette.White), "role.category.modifier"))))
                    ));
                break;
            case HelpTab.Overview:
                widget.Append(ShowPreviewSrceen());
                break;
            case HelpTab.Options:
                widget.Append(ShowOptionsScreen());
                break;
            case HelpTab.Slides:
                widget.Append(ShowSlidesScreen());
                break;
            case HelpTab.Achievements:
                widget.Append(ShowAchievementsScreen());
                break;
        }

        screen.SetWidget(widget);
    }

    static private void ShowDocumentScreen(IDocument doc)
    {
        var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, true);

        Virial.Compat.Artifact<GUIScreen>? inner = null;
        var scrollView = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 4.5f), () => doc.Build(inner) ?? GUIEmptyWidget.Default);
        inner = scrollView.Artifact;
        Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();

        screen.SetWidget(scrollView, out _);
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

        return new VerticalWidgetsHolder(GUIAlignment.Left, widgets);
    }
    static private void OpenAssignableHelp(DefinedAssignable assignable)
    {
        var doc = DocumentManager.GetDocument("role." + assignable.InternalName);
        if (doc == null)
        {
            Debug.Log("Not Existed: " + "role." + assignable.InternalName);
            return;
        }

        ShowDocumentScreen(doc);
    }

    private static IMetaWidgetOld ShowAssignableScreen(params (IEnumerable<DefinedAssignable> assignable, IMetaWidgetOld? header)[] contents)
    {
        MetaWidgetOld inner = new();

        for (int i = 0; i < contents.Length; i++)
        {
            if (contents[i].header != null)
            {
                inner.Append(contents[i].header);
                inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));
            }

            inner.Append(contents[i].assignable, (role) => new MetaWidgetOld.Button(() =>
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

            if (i + 1 != contents.Length)
            {
                inner.Append(new MetaWidgetOld.VerticalMargin(0.2f));
            }
        }

        return new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
    }

    private static IMetaWidgetOld ShowAssignableScreen(IEnumerable<DefinedAssignable> allAssignable)
        => ShowAssignableScreen([(allAssignable, null)]);


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
        var view = new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight), GetOptionsWidget()) { Alignment = IMetaWidgetOld.AlignmentOption.Center, ScrollerTag = "HelpOptions", InnerRef = optionsInner };
        return view;
    }

    static private IMetaWidgetOld GetOptionsWidget()
    {
        StringBuilder builder = new();
        foreach (var holder in ConfigurationHolder.AllHolders.Where(h => h.DisplayOption != Virial.Configuration.ConfigurationHolderState.Inactivated))
        {
            if (!holder.IsShown || !holder.GameModes.Test(GeneralConfigurations.CurrentGameMode)) continue;

            if (builder.Length != 0) builder.Append("\n\n");
            builder.Append(holder.Title.GetString() + "\n  ");
            builder.Append(holder.Configurations.Where(c => c.IsShown).Select(c => c.GetDisplayText()).Where(str => str != null).Join(null, "\n  "));
        }

        return new MetaWidgetOld.VariableText(OptionsAttr) { RawText = builder.ToString(), Alignment = IMetaWidgetOld.AlignmentOption.Center };
    }
    internal static void OnUpdateOptions()
    {
        if (!(optionsInner?.Value?.IsValid ?? false)) return;

        optionsInner.Value.SetWidget(GetOptionsWidget());
    }

    private static IMetaWidgetOld ShowMyRolesSrceen()
    {
        MetaWidgetOld widget = new();

        Virial.Compat.Artifact<GUIScreen> inner = null!;

        widget.Append(PlayerControl.LocalPlayer.GetModInfo()!.AllAssigned().Where(a => a.CanBeAwareAssignment),
            (role) => new MetaWidgetOld.Button(() =>
            {
                var doc = DocumentManager.GetDocument("role." + role.Assignable.InternalName);
                if (doc == null) return;

                inner.Do(screen => screen.SetWidget(doc.Build(inner), out _));
            }, RoleTitleAttrUnmasked)
            {
                RawText = role.DisplayName.Color(role.Assignable.UnityColor),
                Alignment = IMetaWidgetOld.AlignmentOption.Center
            }, 128, -1, 0, 0.6f);

        var scrollView = new GUIScrollView(GUIAlignment.Left, new(7.4f, HelpHeight - 0.7f), () =>
        {
            var doc = DocumentManager.GetDocument("role." + PlayerControl.LocalPlayer.GetModInfo()!.Role.Assignable.InternalName);
            return doc?.Build(inner) ?? GUIEmptyWidget.Default;
        });
        inner = scrollView.Artifact;

        widget.Append(new MetaWidgetOld.WrappedWidget(scrollView));
        
        return widget;
    }

    private static IMetaWidgetOld ShowAchievementsScreen()
    {
        Virial.Media.GUIWidget GenerateWidget(string? scrollerTag = null, Predicate<AbstractAchievement>? predicate = null) => AchievementViewer.GenerateWidget(3.15f, 6.2f, scrollerTag, true, predicate);
        Virial.Compat.Artifact<GUIScreen> artifact = null!;
        List<Virial.Media.GUIWidget> buttons = new([
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.all")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget(), out var _))},
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.achieved")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementAchieved", a => a.IsCleared), out var _))},
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.notAchieved")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementDontAchieved", a => !a.IsCleared), out var _))}
            ]);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized)
        {
            buttons.Add(new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.myRole")) { OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementMyRole", a => NebulaGameManager.Instance.LocalPlayerInfo.AllAssigned().Any(r => r.Assignable == a.Category.role)), out var _)) });
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

    private static IMetaWidgetOld ShowPreviewSrceen()
    {
        var textAttr = GUI.API.GetAttribute(AttributeAsset.OverlayContent);
        var maskedAttr = GUI.API.GetAttribute(AttributeAsset.DocumentStandard);
        Virial.Media.GUIWidget GetAssignableText(DefinedAssignable assignable) => new NoSGUIText(GUIAlignment.Center, maskedAttr, new RawTextComponent(assignable.DisplayColoredName))
        {
            OverlayWidget = () => GetAssignableOverlay(assignable),
            OnClickText = (() => OpenAssignableHelp(assignable), false)
        };

        Virial.Media.GUIWidget GetRoleOverview(RoleCategory category, string categoryName)
        {
            List<Virial.Media.GUIWidget> list100 = new();
            List<Virial.Media.GUIWidget> listRandom = new();
            List<Virial.Media.GUIWidget> ghosts = new();
            List<Virial.Media.GUIWidget> modifiers = new();

            foreach (var role in Roles.Roles.AllRoles.Where(r => r.Category == category))
            {
                if ((role.AllocationParameters?.RoleCount100 ?? 0) > 0)
                {
                    string numText = "x" + role.AllocationParameters!.RoleCount100;
                    if (role.AllocationParameters.RoleCountRandom > 0) numText += $" (+{role.AllocationParameters.RoleCountRandom}, {role.AllocationParameters.GetRoleChance(role.AllocationParameters!.RoleCount100 + 1)}%)";
                    list100.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                }
                else if ((role.AllocationParameters?.RoleCountRandom ?? 0) > 0)
                {
                    string numText = $"x{role.AllocationParameters!.RoleCountRandom} ({role.AllocationParameters.GetRoleChance(1)}%)";
                    listRandom.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                }
            }

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

            List<Virial.Media.GUIWidget> result = [GUI.API.HorizontalMargin(2.4f), GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentTitle), Language.Translate("help.rolePreview.category." + categoryName).Bold())];
            if (list100.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.100") + "-").Bold()), .. list100, GUI.API.Margin(new(2f, 0.3f))]));
            if (listRandom.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.random") + "-").Bold()), .. listRandom, GUI.API.Margin(new(2f, 0.3f))]));
            if (modifiers.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.modifiers") + "-").Bold()), .. modifiers, GUI.API.Margin(new(2f, 0.3f))]));
            if (ghosts.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.ghostRoles") + "-").Bold()), .. ghosts, GUI.API.Margin(new(2f, 0.3f))]));
        
            return GUI.API.VerticalHolder(GUIAlignment.Top, result);
        }


        int players = PlayerControl.AllPlayerControls.Count;
        var flags = AssignmentPreview.CalcPreview(players);
        var iconHolder = GUI.API.HorizontalHolder(GUIAlignment.Center, flags.Select(f => new NoSGUIImage(
            GUIAlignment.Center, RolePreviewIconMap.TryGetValue(f, out var i) ? RolePreviewIconSprite.AsLoader(i) : null!, new(null,0.48f), overlay: () =>
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



        var view = new GUIScrollView(GUIAlignment.Center, new(7.4f, HelpHeight - 0.68f), GUI.API.HorizontalHolder(GUIAlignment.Center, GetRoleOverview(RoleCategory.ImpostorRole,"impostor"), GetRoleOverview(RoleCategory.NeutralRole, "neutral"), GetRoleOverview(RoleCategory.CrewmateRole, "crewmate")));

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

        var mask = UnityHelper.CreateObject<SpriteMask>("Mask", overlay.transform, new Vector3(0, 0, 5f));
        mask.sprite = overlay.sprite;
        mask.transform.localScale = overlay.size;
        
        var hint = AllHints[System.Random.Shared.Next(AllHints.Count)].GUI.Invoke().Instantiate(new(new(0.5f, 0.5f), new(0f, 0f, 0f)), new(6f, 4f), out _);
        hint?.transform.SetParent(overlay.transform);
        if(hint) hint!.transform.localPosition = new(0f, 0f, 10f);

        yield return Effects.ColorFade(overlay, Color.black, Color.clear, 0.5f);
    }

}


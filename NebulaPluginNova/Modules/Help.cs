using System.Text;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;
using Nebula.Behaviour;
using Nebula.Roles.Complex;
using Nebula.Modules.GUIWidget;
using Virial.Media;
using Nebula.Roles;
using Virial.Assignable;
using static Nebula.Modules.MetaWidgetOld;
using Virial.Text;
using NAudio.CoreAudioApi;
using Nebula.Modules.MetaWidget;

namespace Nebula.Modules;

public static class HelpScreen
{
    [Flags]
    public enum HelpTab
    {
        MyInfo = 0x01,
        Roles = 0x02,
        Modifiers = 0x04,
        Options = 0x10,
        Slides = 0x20,
        Achievements = 0x40,
    }

    public record HelpTabInfo(HelpTab Tab,string TranslateKey)
    {
        public MetaWidgetOld.Button GetButton(MetaScreen screen, HelpTab currentTab, HelpTab validTabs) => new(() => ShowScreen(screen, Tab, validTabs), TabButtonAttr) { Color = currentTab == Tab ? Color.white : Color.gray, TranslationKey = TranslateKey };
        
    }

    public static HelpTabInfo[] AllHelpTabInfo = {
        new(HelpTab.MyInfo, "help.tabs.myInfo"),
        new(HelpTab.Roles, "help.tabs.roles"),
        new(HelpTab.Modifiers, "help.tabs.modifiers"),
        new(HelpTab.Options, "help.tabs.options"),
        new(HelpTab.Slides, "help.tabs.slides"),
        new(HelpTab.Achievements, "help.tabs.achievements")
    };

    private static float HelpHeight = 4.1f;

    private static MetaScreen? lastHelpScreen = null;
    public static void TryOpenHelpScreen(HelpTab tab = HelpTab.Roles)
    {
        if (!lastHelpScreen) lastHelpScreen = OpenHelpScreen(tab);
    }

    private static MetaScreen OpenHelpScreen(HelpTab tab)
    {
        var screen = MetaScreen.GenerateWindow(new(7.8f, HelpHeight + 0.6f), HudManager.Instance.transform, new Vector3(0, 0, 0), true, false);

        HelpTab validTabs = HelpTab.Roles | HelpTab.Modifiers | HelpTab.Options | HelpTab.Achievements;

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) validTabs |= HelpTab.MyInfo;
        
        if (AmongUsClient.Instance.AmHost && (NebulaGameManager.Instance?.LobbySlideManager.IsValid ?? false)) validTabs |= HelpTab.Slides;

        //開こうとしているタブが存在しない場合は、ロール一覧を開く
        if ((tab & validTabs) == (HelpTab)0) tab = HelpTab.Roles;

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
                    (Roles.Roles.AllGhostRoles.Where(r => (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), GUI.API.TextComponent(new(Color.gray), "role.category.ghost"))))
                    ));
                break;
            case HelpTab.Modifiers:
                widget.Append(ShowAssignableScreen(Roles.Roles.AllModifiers.Where(m => (m as DefinedAssignable).ShowOnHelpScreen)));
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

    static private void ShowSerializableDocumentScreen(SerializableDocument doc)
    {
        var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, true, true);

        Virial.Compat.Artifact<GUIScreen>? inner = null;
        var scrollView = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 4.5f), () => doc.Build(inner) ?? GUIEmptyWidget.Default);
        inner = scrollView.Artifact;
        Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();

        screen.SetWidget(scrollView, out _);
    }

    private static TextAttributeOld RoleTitleAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static TextAttributeOld RoleTitleAttrUnmasked = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f) };
    
    private static IMetaWidgetOld ShowAssignableScreen(params (IEnumerable<Roles.IAssignableBase> assignable, IMetaWidgetOld? header)[] contents)
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
                var doc = DocumentManager.GetDocument("role." + role.InternalName);
                if (doc == null) return;

                ShowSerializableDocumentScreen(doc);
            }, RoleTitleAttr)
            {
                RawText = role.DisplayName.Color(role.RoleColor),
                PostBuilder = (PassiveButton button, SpriteRenderer renderer, TMPro.TextMeshPro text) =>
                {
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    button.OnMouseOver.AddListener(() => {
                        if (role is IConfiguableAssignable ica)
                        {
                            List<Virial.Media.GUIWidget> widgets = new();

                            widgets.Add(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new RawTextComponent(role.DisplayName.Color(role.RoleColor))));
                            widgets.Add(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new TranslateTextComponent(ica.RoleConfig.Id + ".detail")));
                            if (role is HasCitation hc && hc.Citaion != null)
                            {
                                var citation = hc.Citaion;

                                widgets.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.35f)));
                                widgets.Add(new HorizontalWidgetsHolder(GUIAlignment.Left,
                                    new NoSGUIText(GUIAlignment.Bottom, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new RawTextComponent("from")),
                                    new NoSGUIMargin(GUIAlignment.Bottom, new(0.12f, 0f)),
                                    citation!.LogoImage != null ? GUI.Instance.Image(GUIAlignment.Bottom, citation.LogoImage, new(1.5f, 0.37f)) : new NoSGUIText(GUIAlignment.Left, GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), citation!.Name)));
                            }

                            NebulaManager.Instance.SetHelpWidget(button, new VerticalWidgetsHolder(GUIAlignment.Left, widgets));
                        }
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

    private static IMetaWidgetOld ShowAssignableScreen(IEnumerable<Roles.IAssignableBase> allAssignable)
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
    private static IMetaWidgetOld ShowOptionsScreen()
    {
        MetaWidgetOld inner = new();

        StringBuilder builder = new();
        foreach (var holder in ConfigurationHolder.AllHolders)
        {
            if (!(holder.IsActivated?.Invoke() ?? true) || !holder.IsShown || (holder.GameModeMask & GeneralConfigurations.CurrentGameMode) == 0) continue;

            if (builder.Length != 0) builder.Append("\n");
            holder.GetShownString(ref builder);
        }

        inner.Append(new MetaWidgetOld.VariableText(OptionsAttr) { RawText = builder.ToString(), Alignment = IMetaWidgetOld.AlignmentOption.Center });

        return new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
    }

    private static IMetaWidgetOld ShowMyRolesSrceen()
    {
        MetaWidgetOld widget = new();

        Virial.Compat.Artifact<GUIScreen> inner = null!;

        widget.Append(PlayerControl.LocalPlayer.GetModInfo()!.AllAssigned().Where(a => a.CanBeAwareAssignment),
            (role) => new MetaWidgetOld.Button(() =>
            {
                var doc = DocumentManager.GetDocument("role." + role.AssignableBase.InternalName);
                if (doc == null) return;

                inner.Do(screen => screen.SetWidget(doc.Build(inner), out _));
            }, RoleTitleAttrUnmasked)
            {
                RawText = role.AssignableBase.DisplayName.Color(role.AssignableBase.RoleColor),
                Alignment = IMetaWidgetOld.AlignmentOption.Center
            }, 128, -1, 0, 0.6f);

        var scrollView = new GUIScrollView(GUIAlignment.Left, new(7.4f, HelpHeight - 0.7f), () =>
        {
            var doc = DocumentManager.GetDocument("role." + PlayerControl.LocalPlayer.GetModInfo()!.Role.AssignableBase.InternalName);
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
            buttons.Add(new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievement.filter.myRole")) { OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementMyRole", a => NebulaGameManager.Instance.LocalPlayerInfo.AllAssigned().Any(r => r.AssignableBase == a.Category.role)), out var _)) });
        }

        var sidebar = new VerticalWidgetsHolder(GUIAlignment.Top, buttons);
        var screen = new GUIFixedView(GUIAlignment.Top, new(5.7f, 3.8f), GenerateWidget()) { WithMask = false };
        artifact = screen.Artifact;
        return new MetaWidgetOld.WrappedWidget(new HorizontalWidgetsHolder(GUIAlignment.Center, screen, sidebar));
    }
}

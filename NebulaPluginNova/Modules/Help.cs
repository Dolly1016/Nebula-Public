using System.Text;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;
using Nebula.Behaviour;
using Nebula.Roles.Complex;
using Nebula.Modules.MetaWidget;
using Virial.Media;
using Nebula.Roles;
using Virial.Assignable;
using static Nebula.Modules.MetaWidgetOld;

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
        public MetaContextOld.Button GetButton(MetaScreen screen, HelpTab currentTab, HelpTab validTabs) => new(() => ShowScreen(screen, Tab, validTabs), TabButtonAttr) { Color = currentTab == Tab ? Color.white : Color.gray, TranslationKey = TranslateKey };
        
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

    public static MetaScreen OpenHelpScreen()
    {
        var screen = MetaScreen.GenerateWindow(new(7.8f, HelpHeight + 0.6f), HudManager.Instance.transform, new Vector3(0, 0, 0), true, false);

        HelpTab tab = HelpTab.Roles;
        HelpTab validTabs = HelpTab.Roles | HelpTab.Modifiers | HelpTab.Options | HelpTab.Achievements;

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) {
            validTabs |= HelpTab.MyInfo;
            tab = HelpTab.MyInfo;
        }
        if (AmongUsClient.Instance.AmHost && (NebulaGameManager.Instance?.LobbySlideManager.IsValid ?? false)) validTabs |= HelpTab.Slides;

        ShowScreen(screen,tab,validTabs);

        return screen;
    }

    private static TextAttribute TabButtonAttr = new(TextAttribute.BoldAttr) { Size = new(1.15f, 0.26f) };
    private static IMetaContextOld GetTabsContext(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        List<IMetaParallelPlacableOld> tabs = new();

        foreach (var info in AllHelpTabInfo) if ((validTabs & info.Tab) != 0) tabs.Add(info.GetButton(screen, tab, validTabs));

        return new CombinedContextOld(0.5f,tabs.ToArray());
    }
    private static void ShowScreen(MetaScreen screen, HelpTab tab,HelpTab validTabs)
    {
        MetaContextOld context = new();

        context.Append(GetTabsContext(screen, tab, validTabs));
        context.Append(new MetaContextOld.VerticalMargin(0.1f));

        switch (tab)
        {
            case HelpTab.MyInfo:
                context.Append(ShowMyRolesSrceen());
                break;
            case HelpTab.Roles:
                widget.Append(ShowAssignableScreen<AbstractRole>(
                    (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.ImpostorRole && (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), new TranslateTextComponent("role.category.impostor")))),
                    (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.NeutralRole && (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), new TranslateTextComponent("role.category.neutral")))),
                    (Roles.Roles.AllRoles.Where(r => r.Category == RoleCategory.CrewmateRole && (r as DefinedAssignable).ShowOnHelpScreen), new WrappedWidget(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentTitle), new TranslateTextComponent("role.category.crewmate"))))
                    ));
                break;
            case HelpTab.Modifiers:
                widget.Append(ShowAssignableScreen(Roles.Roles.AllModifiers.Where(m => (m as DefinedAssignable).ShowOnHelpScreen)));
                break;
            case HelpTab.Options:
                context.Append(ShowOptionsScreen());
                break;
            case HelpTab.Slides:
                context.Append(ShowSlidesScreen());
                break;
            case HelpTab.Achievements:
                context.Append(ShowAchievementsScreen());
                break;
        }

        screen.SetContext(context);
    }

    private static TextAttribute RoleTitleAttr = new TextAttribute(TextAttribute.BoldAttr) { Size = new Vector2(1.4f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static TextAttribute RoleTitleAttrUnmasked = new TextAttribute(TextAttribute.BoldAttr) { Size = new Vector2(1.4f, 0.29f) };
    private static IMetaContextOld ShowAssignableScreen<Assignable>(IEnumerable<Assignable> allAssignable) where Assignable : Roles.IAssignableBase
    {
        MetaContextOld inner = new();

        Virial.Compat.Artifact<GUIScreen>? inner = null;
        var scrollView = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 4.5f), () => doc.Build(inner) ?? GUIEmptyWidget.Default);
        inner = scrollView.Artifact;
        Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();

        screen.SetWidget(scrollView, out _);
    }

    private static TextAttributeOld RoleTitleAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static TextAttributeOld RoleTitleAttrUnmasked = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f) };
    
    private static IMetaWidgetOld ShowAssignableScreen<Assignable>(params (IEnumerable<Assignable> assignable, IMetaWidgetOld? header)[] contents) where Assignable : Roles.IAssignableBase
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
                PostBuilder = (button, renderer, text) =>
                {
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    button.OnMouseOver.AddListener(() => {
                        if (role is IConfiguableAssignable ica)
                        {
                            List<GUIWidget> widgets = new();

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

    private static IMetaWidgetOld ShowAssignableScreen<Assignable>(IEnumerable<Assignable> allAssignable) where Assignable : Roles.IAssignableBase
        => ShowAssignableScreen([(allAssignable, null)]);


    private static TextAttribute SlideTitleAttr = new(TextAttribute.NormalAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(3.6f, 0.28f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static TextAttribute SlideButtonAttr = new(TextAttribute.BoldAttr) { Size = new(0.8f, 0.25f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static IMetaContextOld ShowSlidesScreen()
    {
        MetaContextOld inner = new();

        foreach (var temp in LobbySlideManager.AllTemplates)
        {
            var copiedTemp = temp;
            inner.Append(new CombinedContextOld(
                0.5f,
                new MetaContextOld.Text(SlideTitleAttr) { RawText = temp.Title },
                new MetaContextOld.HorizonalMargin(0.2f),
                new MetaContextOld.Button(()=> NebulaGameManager.Instance?.LobbySlideManager.TryRegisterAndShow(copiedTemp?.Generate()), SlideButtonAttr) { TranslationKey = "help.slides.share", PostBuilder = (_,renderer,_)=>renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask }
                ));
        }

        return new MetaContextOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaContextOld.AlignmentOption.Center };
    }

    private static TextAttribute OptionsAttr = new(TextAttribute.BoldAttr) { FontSize = 1.6f, FontMaxSize = 1.6f, FontMinSize = 1.6f, Size = new(4f, 10f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Alignment = TMPro.TextAlignmentOptions.TopLeft };
    private static IMetaContextOld ShowOptionsScreen()
    {
        MetaContextOld inner = new();

        StringBuilder builder = new();
        foreach (var holder in ConfigurationHolder.AllHolders)
        {
            if (!(holder.IsActivated?.Invoke() ?? true) || !holder.IsShown || (holder.GameModeMask & GeneralConfigurations.CurrentGameMode) == 0) continue;

            if (builder.Length != 0) builder.Append("\n");
            holder.GetShownString(ref builder);
        }

        inner.Append(new MetaContextOld.VariableText(OptionsAttr) { RawText = builder.ToString(), Alignment = IMetaContextOld.AlignmentOption.Center });

        return new MetaContextOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaContextOld.AlignmentOption.Center };
    }

    private static IMetaContextOld ShowMyRolesSrceen()
    {
        MetaContextOld context = new();
        Reference<MetaContextOld.ScrollView.InnerScreen> innerRef = new();

        context.Append(PlayerControl.LocalPlayer.GetModInfo()!.AllAssigned().Where(a => a.CanBeAwareAssignment),
            (role) => new MetaContextOld.Button(() =>
            {
                var doc = DocumentManager.GetDocument("role." + role.AssignableBase.InternalName);
                if (doc == null) return;

                innerRef.Value!.SetContext(doc.Build(innerRef));
            }, RoleTitleAttrUnmasked)
            {
                RawText = role.AssignableBase.DisplayName.Color(role.AssignableBase.RoleColor),
                Alignment = IMetaContextOld.AlignmentOption.Center
            }, 128, -1, 0, 0.6f);

        context.Append(new MetaContextOld.ScrollView(new(7.4f, HelpHeight - 0.7f), new MetaContextOld()) { Alignment = IMetaContextOld.AlignmentOption.Center, InnerRef = innerRef,
        PostBuilder = ()=> {
            innerRef.Value!.SetContext(DocumentManager.GetDocument("role." + PlayerControl.LocalPlayer.GetModInfo()!.Role.AssignableBase.InternalName)?.Build(innerRef));
        }
        });
        

        return context;
    }

    private static IMetaContextOld ShowAchievementsScreen()
    {
        return new MetaContextOld.WrappedContext(AchievementViewer.GenerateContext(3.15f, 7.8f));
    }
}

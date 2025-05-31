using Hazel.Crypto;
using NAudio.CoreAudioApi;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using Nebula.Roles;
using Nebula.Roles.Assignment;
using Nebula.Roles.Neutral;
using Nebula.Utilities;
using Sentry.Unity.NativeUtils;
using Steamworks;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine.Rendering;
using Virial;
using Virial.Assignable;
using Virial.Game;
using Virial.Media;
using Virial.Text;
using Virial.Utilities;
using static Nebula.Modules.MetaWidgetOld;
using AssignmentFlag = Nebula.Roles.Assignment.AssignmentPreview.AssignmentFlag;
namespace Nebula.Modules;

public class CombiImageInfo {
    public static readonly Dictionary<string, CombiImageInfo> FastImages = [];
    public static readonly OrderedList<CombiImageInfo, int> OrderedImages = OrderedList<CombiImageInfo, int>.DescendingList(image => image.Priority);

    public static readonly CombiImageInfo SniperRaider = new("SniperRaider", 10, () => CheckSpawnable(Roles.Impostor.Sniper.MyRole, Roles.Impostor.Raider.MyRole));
    public static readonly CombiImageInfo JesNecRep = new("NecroJesterReaper", 50, () => CheckSpawnable(Roles.Neutral.Jester.MyRole, Roles.Impostor.Reaper.MyRole, Roles.Crewmate.Necromancer.MyRole));
    public static readonly CombiImageInfo JesNec = new("NecroJester", 40, () => CheckSpawnable(Roles.Neutral.Jester.MyRole, Roles.Crewmate.Necromancer.MyRole));
    public static readonly CombiImageInfo JesRep = new("JesterReaper", 40, () => CheckSpawnable(Roles.Neutral.Jester.MyRole, Roles.Impostor.Reaper.MyRole));
    public static readonly CombiImageInfo SpectreImmoralist = new("SpectreAndImmoralist", 44, () => CheckSpawnable(Roles.Neutral.SpectreImmoralist.MyRole, Roles.Neutral.Spectre.MyRole));
    public static readonly CombiImageInfo SpectreAndFollower = new("SpectreAndFollower", 45, () => CheckSpawnable(Roles.Neutral.SpectreFollower.MyRole, Roles.Neutral.Spectre.MyRole));


    public string Name { get; private init; }
    public Image Image { get; private init; }
    public int Priority { get; private init; }
    public Func<bool> Predicate { get; private init; }
    private CombiImageInfo(string imageName, int priority, Func<bool> predicate)
    {
        this.Name = imageName;
        this.Image = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Combinations/" + imageName + ".png");
        this.Priority = priority;
        this.Predicate = predicate;
        FastImages[imageName] = this;
        OrderedImages.Add(this);
    }

    private static bool CheckSpawnable(params ISpawnable[] spawnables)
    {
        return spawnables.All(s => s.IsSpawnable && (s is not DefinedRole dr || GeneralConfigurations.exclusiveAssignmentOptions.All(option => !option.OnAssigned(dr).Any(r => spawnables.Contains(r)))));
    }

    public static bool TryGetSuitableImage([MaybeNullWhen(false)]out CombiImageInfo info)
    {
        info = null;
        foreach(var cand in OrderedImages)
        {
            if (cand.Predicate.Invoke())
            {
                info = cand;
                return true;
            }
        }
        return false;
    }
}
public static class HelpScreen
{
    public class HelpArgument
    {
        public int PreviewSimulation = -1;
        public bool CanCloseEasily = false;

        public HelpArgument() { }
        static public HelpArgument Default => new();
    }

    [Flags]
    public enum HelpTab
    {
        MyInfo = 0x01,
        Roles = 0x02,
        Options = 0x04,
        Slides = 0x08,
        Achievements = 0x10,
        Overview = 0x20,
        Stamps = 0x40,
    }

    public record HelpTabInfo(HelpTab Tab,string TranslateKey)
    {
        public MetaWidgetOld.Button GetButton(MetaScreen screen, HelpTab currentTab, HelpTab validTabs) => new(() => ShowScreen(screen, HelpArgument.Default, Tab, validTabs), TabButtonAttr) { Color = currentTab == Tab ? Color.white : Color.gray, TranslationKey = TranslateKey };
        
    }

    public static readonly HelpTabInfo[] AllHelpTabInfo = [
        new(HelpTab.MyInfo, "help.tabs.myInfo"),
        new(HelpTab.Roles, "help.tabs.roles"),
        new(HelpTab.Overview, "help.tabs.overview"),
        new(HelpTab.Options, "help.tabs.options"),
        new(HelpTab.Slides, "help.tabs.slides"),
        new(HelpTab.Achievements, "help.tabs.achievements"),
        new(HelpTab.Stamps, "help.tabs.stamps"),
    ];

    private static float HelpHeight = 4.1f;

    private static MetaScreen? lastHelpScreen = null;

    //Current help status
    private static HelpArgument lastArgument = HelpArgument.Default;
    private static HelpTab lastTab = HelpTab.Roles;

    public static bool OpenedAnyHelpScreen => lastHelpScreen;
    public static MetaScreen? LastHelpScreen => lastHelpScreen;

    public static void TryOpenHelpScreen(HelpTab tab, HelpArgument? argument = null)
    {
        if (!lastHelpScreen) lastHelpScreen = OpenHelpScreen(tab, argument ?? HelpArgument.Default);
    }
    public static void TryCloseHelpScreen()
    {
        if (lastHelpScreen) lastHelpScreen!.CloseScreen();
    }
    private static MetaScreen OpenHelpScreen(HelpTab tab, HelpArgument argument)
    {
        var screen = MetaScreen.GenerateWindow(new(7.8f, HelpHeight + 0.6f), HudManager.Instance.transform, new Vector3(0, 0, 0), true, false, background: BackgroundSetting.Modern);

        HelpTab validTabs = HelpTab.Roles | HelpTab.Overview | HelpTab.Options | HelpTab.Achievements | HelpTab.Stamps;

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) validTabs |= HelpTab.MyInfo;
        
        if (AmongUsClient.Instance.AmHost && (NebulaGameManager.Instance?.LobbySlideManager.IsValid ?? false)) validTabs |= HelpTab.Slides;

        //開こうとしているタブが存在しない場合は、ロール一覧を開く
        if ((tab & validTabs) == (HelpTab)0) tab = PlayerControl.AllPlayerControls.Count > 5 ? HelpTab.Overview : HelpTab.Roles;

        ShowScreen(screen, argument, tab, validTabs);

        return screen;
    }

    private static TextAttributeOld TabButtonAttr = new(TextAttributeOld.BoldAttr) { Size = new(1f, 0.26f) };
    private static IMetaWidgetOld GetTabsWidget(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        List<IMetaParallelPlacableOld> tabs = [];

        foreach (var info in AllHelpTabInfo) if ((validTabs & info.Tab) != 0) tabs.Add(info.GetButton(screen, tab, validTabs));

        return new CombinedWidgetOld(0.5f,tabs.ToArray());
    }
    private static void ShowScreen(MetaScreen screen, HelpArgument argument, HelpTab tab,HelpTab validTabs)
    {
        lastArgument = argument;
        lastTab = tab;

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
                widget.Append(ShowPreviewScreen(screen, validTabs, out backImage));
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
            case HelpTab.Stamps:
                widget.Append(ShowStampsScreen());
                break;
        }

        screen.SetWidget(widget);
        screen.SetBackImage(backImage, 0.2f);
    }

    static private void ShowDocumentScreen(IDocument doc, Image? illustration)
    {
        var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, true, background: BackgroundSetting.Modern);

        Virial.Compat.Artifact<GUIScreen>? inner = null;
        var scrollView = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 4.5f), () => doc.Build(inner) ?? GUIEmptyWidget.Default);
        inner = scrollView.Artifact;
        Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();

        screen.SetWidget(scrollView, illustration, out _);
    }

    private static readonly TextAttributeOld RoleTitleAttr = new(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    private static readonly TextAttributeOld RoleTitleAttrUnmasked = new(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.29f) };
    
    private static Virial.Media.GUIWidget GetAssignableOverlay(DefinedAssignable assignable)
    {
        List<Virial.Media.GUIWidget> widgets = [];

        widgets.Add(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new RawTextComponent(assignable.DisplayColoredName)));
        widgets.Add(new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), assignable?.ConfigurationHolder?.Detail));
        if (assignable is HasCitation hc && hc.Citation != null)
        {
            var citation = hc.Citation;

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

        foreach (var temp in LobbySlideManager.AllTemplates.Values)
        {
            if (temp.IsHidden) continue;

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

    private static readonly TextAttributeOld OptionsAttr = new(TextAttributeOld.BoldAttr) { FontSize = 1.6f, FontMaxSize = 1.6f, FontMinSize = 1.6f, Size = new(4f, 10f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Alignment = TMPro.TextAlignmentOptions.TopLeft };
    private static Reference<ScrollView.InnerScreen>? optionsInner = new();
    private static IMetaWidgetOld ShowOptionsScreen()
    {
        var view = new MetaWidgetOld.ScrollView(new(7.4f, HelpHeight - 0.5f), GetOptionsWidget()) { Alignment = IMetaWidgetOld.AlignmentOption.Center, ScrollerTag = "HelpOptions", InnerRef = optionsInner };
        List<Virial.Media.GUIWidget> buttons = [];
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
                    outsideScreen.SetBackImage(role.ConfigurationHolder?.Illustration, 0.2f);
                });
            }, RoleTitleAttrUnmasked)
            {
                RawText = role.DisplayColoredName,
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

    private static IMetaWidgetOld ShowAchievementsScreen()
    {
        Virial.Media.GUIWidget GenerateWidget(string? scrollerTag = null, Predicate<INebulaAchievement>? predicate = null, string? shownText = null) => AchievementViewer.GenerateWidget(3.15f, 6.2f, scrollerTag, true, predicate, shownText, onClicked: lastArgument.CanCloseEasily ? _ => TryCloseHelpScreen() : null);
        Virial.Compat.Artifact<GUIScreen> artifact = null!;
        
        List<Virial.Media.GUIWidget> buttons = new([
            GUI.API.LocalizedText(GUIAlignment.Center, AttributeAsset.StandardMediumMasked, "achievement.filter"),
            new GUIModernButton(GUIAlignment.Center, AttributeAsset.CenteredBoldFixed, new TranslateTextComponent("achievement.filter.all")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget(), out var _)), SelectedDefault = true, WithCheckMark = true },
            new GUIModernButton(GUIAlignment.Center, AttributeAsset.CenteredBoldFixed, new TranslateTextComponent("achievement.filter.achieved")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementAchieved", a => a.IsCleared, Language.Translate("achievement.filter.achieved")), out var _)), WithCheckMark = true},
            new GUIModernButton(GUIAlignment.Center, AttributeAsset.CenteredBoldFixed, new TranslateTextComponent("achievement.filter.nonAchieved")){ OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementDontAchieved", a => !a.IsCleared, Language.Translate("achievement.filter.nonAchieved")), out var _)), WithCheckMark = true},
            new GUIModernButton(GUIAlignment.Center, AttributeAsset.CenteredBoldFixed, new TranslateTextComponent("achievement.filter.search")){ OnClick = clickable => {
                var searchWindow = MetaScreen.GenerateWindow(new(6f,0.8f), HudManager.Instance.transform, Vector3.zero, true, true, true);

                void ShowResult(string rawKeyword){
                    string[] keyword = rawKeyword.Split(' ','　').Where(s => s.Length >= 1).ToArray();
                    searchWindow.CloseScreen();
                    clickable.Selectable?.Select();

                    artifact.Do(screen => screen.SetWidget(GenerateWidget(predicate: ac => keyword.All(k => ac.GetKeywords().Any(acK => acK.Contains(k))), shownText: Language.Translate("achievement.ui.shown.search").Replace("%KEYWORD%", rawKeyword)), out var _));
                }

                var textField = new GUITextField(GUIAlignment.Left, new(4.3f,0.4f)){ HintText = Language.Translate("ui.dialog.keyword").Color(Color.gray), IsSharpField = false, WithMaskMaterial = true, EnterAction = (rawKeyword) => {ShowResult(rawKeyword); return true; } };
                var button = new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("ui.dialog.search")){OnClick = _ =>ShowResult(textField.Artifact.FirstOrDefault()?.Text ?? "")};
                searchWindow.SetWidget(GUI.API.HorizontalHolder(GUIAlignment.Center, textField, button), new Vector2(0.5f,0.5f), out var size);
                textField.Artifact.Do(field => field.GainFocus());
            }, WithCheckMark = true, BlockSelectingOnClicked = true},
            ]);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized)
        {
            buttons.Add(new GUIModernButton(GUIAlignment.Center, AttributeAsset.CenteredBoldFixed, new TranslateTextComponent("achievement.filter.myRole")) { OnClick = _ => artifact.Do(screen => screen.SetWidget(GenerateWidget("AchievementMyRole", a => GamePlayer.LocalPlayer.AllAssigned().Any(r => r.Assignable == a.RelatedRole), Language.Translate("achievement.filter.myRole")), out var _)), WithCheckMark = true });
        }

        var sidebar = new GUIButtonGroup(new VerticalWidgetsHolder(GUIAlignment.Top, buttons));
        var screen = new GUIFixedView(GUIAlignment.Top, new(5.7f, 3.8f), GenerateWidget()) { WithMask = false };
        artifact = screen.Artifact;
        return new MetaWidgetOld.WrappedWidget(new HorizontalWidgetsHolder(GUIAlignment.Center, screen, sidebar));
    }

    private static IDividedSpriteLoader RolePreviewIconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.RolePreviewIcon.png", 100f, 10, 2);
    private static Dictionary<AssignmentFlag, int> RolePreviewIconMap = new Dictionary<AssignmentFlag, int>()
    {
        { AssignmentFlag.VanillaImpostor, 0},
        { AssignmentFlag.ModImpostor, 1},
        { AssignmentFlag.VanillaImpostor | AssignmentFlag.ModImpostor, 2},
        { AssignmentFlag.ModNeutral, 3},
        { AssignmentFlag.ModCrewmate | AssignmentFlag.ModNeutral, 4},
        { AssignmentFlag.VanillaCrewmate | AssignmentFlag.ModNeutral, 5},
        { AssignmentFlag.VanillaCrewmate | AssignmentFlag.ModCrewmate | AssignmentFlag.ModNeutral, 6},
        { AssignmentFlag.ModCrewmate, 7},
        { AssignmentFlag.VanillaCrewmate | AssignmentPreview.AssignmentFlag.ModCrewmate, 8},
        { AssignmentFlag.VanillaCrewmate, 9},
        { AssignmentFlag.ModImpostor | AssignmentFlag.ModNeutral, 10},
        { AssignmentFlag.ModImpostor | AssignmentFlag.VanillaCrewmate | AssignmentFlag.ModCrewmate, 11},
        { AssignmentFlag.ModImpostor | AssignmentFlag.ModCrewmate, 12},
        { AssignmentFlag.ModImpostor | AssignmentFlag.VanillaCrewmate, 13},
        { AssignmentFlag.ModImpostor | AssignmentFlag.ModNeutral | AssignmentFlag.VanillaCrewmate, 14},
        { AssignmentFlag.ModImpostor | AssignmentFlag.ModNeutral | AssignmentFlag.ModCrewmate, 15},
        { AssignmentFlag.ModImpostor | AssignmentFlag.ModNeutral | AssignmentFlag.VanillaCrewmate | AssignmentFlag.ModCrewmate, 16},
    };

    private static IMetaWidgetOld ShowPreviewScreen(MetaScreen screen, HelpTab validTabs, out Image? backImage)
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


        //付随出現役職を格納するリスト
        List<Virial.Media.GUIWidget> listAdditionalImp = [], listAdditionalNeu = [], listAdditionalCrew = [];
        void AddAdditionalRole(DefinedRole additionalRole, DefinedRole reason)
        {
            var holder = GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(additionalRole, null), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, $"({reason.DisplayColoredName})"));
            var list = additionalRole.Category switch { 
                RoleCategory.CrewmateRole => listAdditionalCrew,
                RoleCategory.NeutralRole => listAdditionalNeu,
                RoleCategory.ImpostorRole => listAdditionalImp,
                _ => null
                };
            list?.Add(holder);
        }

        Virial.Media.GUIWidget GetRoleOverview(RoleCategory category, string categoryName, List<Virial.Media.GUIWidget> additionalList, bool with100View, bool withRandomView, bool withJackalizedView = false)
        {
            List<Virial.Media.GUIWidget> list100 = [], listRandom = [], ghosts = [], modifiers = [];

            void CheckRoles(List<Virial.Media.GUIWidget> list100, List<Virial.Media.GUIWidget> listRandom, bool jackalAssignment = false)
            {
                foreach (var role in Roles.Roles.AllRoles.Where(r => jackalAssignment || r.Category == category))
                {
                    var param = jackalAssignment ? role.JackalAllocationParameters : role.AllocationParameters;
                    if ((param?.RoleCount100 ?? 0) > 0)
                    {
                        if (with100View)
                        {
                            string numText = "x" + param!.RoleCount100;
                            if (param.RoleCountRandom > 0) numText += $" (+{param.RoleCountRandom}, {param.GetRoleChance(param!.RoleCount100 + 1)}%)";
                            list100.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role, jackalAssignment ? role.DisplayName.Color(Jackal.MyRole.UnityColor) : null), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                            if (!jackalAssignment) role.AdditionalRoles.Do(a => AddAdditionalRole(a, role));
                        }
                    }
                    else if ((param?.RoleCountRandom ?? 0) > 0)
                    {
                        if (withRandomView)
                        {
                            string numText = $"x{param!.RoleCountRandom} ({param.GetRoleChance(1)}%)";
                            listRandom.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role, jackalAssignment ? role.DisplayName.Color(Jackal.MyRole.UnityColor) : null), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
                            if (!jackalAssignment) role.AdditionalRoles.Do(a => AddAdditionalRole(a, role));
                        }
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
            result.Add(new LazyGUIWidget(GUIAlignment.Center, () => additionalList.IsEmpty() ? GUIEmptyWidget.Default : GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.additional") + "-").Bold()), ..additionalList, GUI.API.Margin(new(2f, 0.3f))])));
            if (modifiers.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.modifiers") + "-").Bold()), .. modifiers, GUI.API.Margin(new(2f, 0.3f))]));
            if (ghosts.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.ghostRoles") + "-").Bold()), .. ghosts, GUI.API.Margin(new(2f, 0.3f))]));
            if (withJackalizedView)
            {
                List<Virial.Media.GUIWidget> listJ100 = [];
                List<Virial.Media.GUIWidget> listJRandom = [];
                CheckRoles(listJ100, listJRandom, true);

                if (listJ100.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.jackalized.100") + "-").Bold()), .. listJ100, GUI.API.Margin(new(2f, 0.3f))]));
                if (listJRandom.Count > 0) result.Add(GUI.API.VerticalHolder(GUIAlignment.Center, [GUI.API.RawText(GUIAlignment.Center, maskedAttr, ("-" + Language.Translate("help.rolePreview.inner.jackalized.random") + "-").Bold()), .. listJRandom, GUI.API.Margin(new(2f, 0.3f))]));
            }

            return GUI.API.VerticalHolder(GUIAlignment.Top, result);
        }


        int players = lastArgument.PreviewSimulation == -1 ? PlayerControl.AllPlayerControls.Count : lastArgument.PreviewSimulation;
        var flags = AssignmentPreview.CalcPreview(players);
        
        //出現しうるカテゴリを全て集計する。
        AssignmentPreview.AssignmentFlag allFlag = 0;
        flags.Do(f => allFlag |= f);

        var iconHolder = GUI.API.HorizontalHolder(GUIAlignment.Center, flags.Select(f => new NoSGUIImage(
            GUIAlignment.Center, RolePreviewIconMap.TryGetValue(f & AssignmentFlag.ImageFlag, out var i) ? RolePreviewIconSprite.AsLoader(i) : null!, new(null,flags.Length >= 18 ? 0.33f : 0.48f), overlay: () =>
            {
                string text = Language.Translate("help.rolePreview.header").Bold() + "<br>";
                if ((f & AssignmentFlag.ModImpostor100) != 0) text += "<br>" + Language.Translate("help.rolePreview.modImpostor.100").Color(Palette.ImpostorRed);
                if ((f & AssignmentFlag.ModImpostorPrb) != 0) text += "<br>" + Language.Translate("help.rolePreview.modImpostor.random").Color(Palette.ImpostorRed);
                if ((f & AssignmentFlag.ModImpostorAdd) != 0) text += "<br>" + Language.Translate("help.rolePreview.modImpostor.additional").Color(Palette.ImpostorRed);
                if ((f & AssignmentFlag.VanillaImpostor) != 0) text += "<br>" + Language.Translate("help.rolePreview.vanillaImpostor").Color(Palette.ImpostorRed);
                if ((f & AssignmentFlag.ModNeutral100) != 0) text += "<br>" + Language.Translate("help.rolePreview.modNeutral.100").Color(Color.yellow);
                if ((f & AssignmentFlag.ModNeutralPrb) != 0) text += "<br>" + Language.Translate("help.rolePreview.modNeutral.random").Color(Color.yellow);
                if ((f & AssignmentFlag.ModNeutralAdd) != 0) text += "<br>" + Language.Translate("help.rolePreview.modNeutral.additional").Color(Color.yellow);
                if ((f & AssignmentFlag.ModCrewmate100) != 0) text += "<br>" + Language.Translate("help.rolePreview.modCrewmate.100").Color(Palette.CrewmateBlue);
                if ((f & AssignmentFlag.ModCrewmatePrb) != 0) text += "<br>" + Language.Translate("help.rolePreview.modCrewmate.random").Color(Palette.CrewmateBlue);
                if ((f & AssignmentFlag.ModCrewmateAdd) != 0) text += "<br>" + Language.Translate("help.rolePreview.modCrewmate.additional").Color(Palette.CrewmateBlue);
                if ((f & AssignmentFlag.VanillaCrewmate) != 0) text += "<br>" + Language.Translate("help.rolePreview.vanillaCrewmate").Color(Palette.CrewmateBlue);

                return GUI.API.RawText(GUIAlignment.Center, textAttr, text);
            })
            ), 0.48f);

        List<Virial.Media.GUIWidget> winConds = [
            GUI.API.RawText(GUIAlignment.Left, maskedTitleAttr,"勝利条件"),
            ];
        GameEnd? end = null;
        foreach(var tip in DocumentTipManager.WinConditionTips)
        {
            if (end != tip.End)
            {
                winConds.Add(GUI.API.VerticalMargin(0.1f));
                winConds.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.2f), GUI.API.RawText(GUIAlignment.Left, maskedSubtitleAttr, tip.End.DisplayText.GetString().Replace("%EXTRA%", "").Color(tip.End.Color))));
                end = tip.End;
            }
            winConds.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.3f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, ("・" + tip.Title).Bold())));
            winConds.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.45f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, tip.Text)));
            winConds.Add(GUI.API.VerticalMargin(0.15f));
        }

        Virial.Media.GUIWidget? specialAssignmentsWidget = null;
        List<Virial.Media.GUIWidget> specialAssignments = [];
        void AddSpecialAssignment(DefinedAssignable assignable, int num, int percentage)
        {
            string numText = "x" + num;
            if (percentage < 100) numText += $" ({percentage}%)";
            specialAssignments.Add(GUI.API.HorizontalHolder(GUIAlignment.Left, GetAssignableText(assignable), GUI.API.HorizontalMargin(0.1f), GUI.API.RawText(GUIAlignment.Left, maskedAttr, numText)));
        }
        if(Roles.Modifier.Lover.NumOfPairsOption > 0) AddSpecialAssignment(Roles.Modifier.Lover.MyRole, Roles.Modifier.Lover.NumOfPairsOption, Roles.Modifier.Lover.RoleChanceOption);
        if (Roles.Modifier.Trilemma.NumOfTrilemmaOption > 0) AddSpecialAssignment(Roles.Modifier.Trilemma.MyRole, Roles.Modifier.Trilemma.NumOfTrilemmaOption, Roles.Modifier.Trilemma.RoleChanceOption);
        if(specialAssignments.Count > 0)
        {
            specialAssignmentsWidget = GUI.API.VerticalHolder(GUIAlignment.Center, [
                GUI.API.HorizontalMargin(2f),
                GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentTitle), Language.Translate("help.rolePreview.category.special").Bold()),
                ..specialAssignments
                ]);
            ;
        }

        var view = new GUIScrollView(GUIAlignment.Center, new(7.4f, HelpHeight - 0.68f - 0.5f),
            GUI.API.HorizontalHolder(GUIAlignment.Left, GUI.API.HorizontalMargin(0.35f),
            GUI.API.VerticalHolder(GUIAlignment.Center,
                GUI.API.HorizontalHolder(GUIAlignment.Center, 
                GetRoleOverview(RoleCategory.ImpostorRole,"impostor", listAdditionalImp, allFlag.HasFlag(AssignmentFlag.ModImpostor100), allFlag.HasFlag(AssignmentFlag.ModImpostorPrb)), 
                GeneralConfigurations.NeutralSpawnable ? GetRoleOverview(RoleCategory.NeutralRole, "neutral", listAdditionalNeu, allFlag.HasFlag(AssignmentFlag.ModNeutral100), allFlag.HasFlag(AssignmentFlag.ModNeutralPrb), (Jackal.MyRole as DefinedRole).IsSpawnable && Jackal.JackalizedImpostorOption) : GUI.API.EmptyWidget, 
                GetRoleOverview(RoleCategory.CrewmateRole, "crewmate", listAdditionalCrew, allFlag.HasFlag(AssignmentFlag.ModCrewmate100), allFlag.HasFlag(AssignmentFlag.ModCrewmatePrb))),
                specialAssignmentsWidget,
                GUI.API.VerticalHolder(GUIAlignment.Left, winConds)
            )));

        //背景画像の選定
        bool CheckSpawnable(params ISpawnable[] spawnables)
        {
            return spawnables.All(s => s.IsSpawnable && (s is not DefinedRole dr || GeneralConfigurations.exclusiveAssignmentOptions.All(option => !option.OnAssigned(dr).Any(r => spawnables.Contains(r)))));
        }

        CombiImageInfo.TryGetSuitableImage(out var info);
        backImage = info?.Image;

        int GetNextSimulatePlayerArgument(bool increment)
        {
            if(lastArgument.PreviewSimulation == -1)
            {
                return AmongUsUtil.NumOfImpostors switch
                {
                    5 => 22,
                    4 => 18,
                    3 => 14,
                    2 => 11,
                    _ => 7,
                };
            }
            else
            {
                return Math.Clamp(lastArgument.PreviewSimulation + (increment ? 1 : -1), 1, 24);
            }
        }

        TMPro.TextMeshPro simulateText = null!;
        return new MetaWidgetOld.WrappedWidget(new VerticalWidgetsHolder(GUIAlignment.Center, 
            iconHolder, 
            GUI.API.HorizontalHolder(GUIAlignment.Right,
            GUI.API.RawButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.SmallArrowButton), "<<", _ =>
            {
                lastArgument.PreviewSimulation = GetNextSimulatePlayerArgument(false);
                ShowScreen(screen, lastArgument, HelpTab.Overview, validTabs);
            }),
            new NoSGUIText(GUIAlignment.Right, GUI.API.GetAttribute(AttributeAsset.SmallWideButton),
            lastArgument.PreviewSimulation == -1 ? 
            new TranslateTextComponent("help.overview.pattern.realCount") : 
            new RawTextComponent(Language.Translate("help.overview.pattern.custom").Replace("%NUM%", players.ToString()))) { PostBuilder = t => simulateText = t},
            GUI.API.RawButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.SmallArrowButton), ">>", _ =>
            {
                lastArgument.PreviewSimulation = GetNextSimulatePlayerArgument(true);
                ShowScreen(screen, lastArgument, HelpTab.Overview, validTabs);
            })
            ),
            new NoSGUIMargin(GUIAlignment.Center, new(0f, 0.05f)),
            view));
    }

    private static IMetaWidgetOld ShowStampsScreen()
    {
        List<Virial.Media.GUIWidget> widgets = [GUI.API.HorizontalMargin(7.4f)];

        UnityEngine.Color selectedColor = new(1f, 0.22f, 0.22f);
        string? selectedTableStamp = null;
        (string prodId, SpriteRenderer backRenderer)? selectedInList = null;
        List<(string prodId, SpriteRenderer backRenderer)> activeStamps = [];

        GUIFixedView upperView = null!;
        void UpdateUpperScreen() => upperView.Artifact.Do(a => a.SetWidget(GetUpperScreen(), out var _));
        Virial.Media.GUIWidget GetUpperScreen() =>
            GUI.API.VerticalHolder(GUIAlignment.Center,
            GUI.API.HorizontalMargin(7.8f),
            GUI.API.HorizontalHolder(GUIAlignment.Center,
            StampManager.CurrentTable.Select(id => {
                bool found = MoreCosmic.AllStamps.TryGetValue(id, out var stamp);
                return (id, found ? stamp : null);
            }).Select(entry =>
            {
                (var id, var stamp) = entry;
                return new NoSGUIFramed(GUIAlignment.Center, stamp.GetStampWidget(id, PlayerControl.LocalPlayer.PlayerId, GUIAlignment.Center, false, 0.55f, _ =>
                {
                    //OnClicked
                    if (selectedInList == null)
                    {
                        var text = Language.Translate("ui.stamp.select");
                        DebugScreen.Push(new FunctionalDebugTextContent(() => text, FunctionalLifespan.GetTimeLifespan(3f)));

                        selectedTableStamp = selectedTableStamp == id ? null : id;
                    }
                    else
                    {
                        StampManager.SetToTable(id, selectedInList.Value.prodId);
                        activeStamps.Add((selectedInList.Value.prodId, selectedInList.Value.backRenderer));
                        selectedInList = null;
                        UpdateActiveStamps(true);
                    }

                    UpdateUpperScreen();
                },
                () => stamp.GetStampLabelWidget(id, Language.Translate(selectedInList == null ? "ui.stamp.click.top.1" : "ui.stamp.click.top.2"))
                ), new(0.05f, 0.05f), (id == selectedTableStamp) ? selectedColor : Color.clear);
            })));
        upperView = new GUIFixedView(GUIAlignment.Center, new(7.8f, 0.8f), GetUpperScreen);

        //装備されているスタンプにのみ色を付ける。
        void UpdateActiveStamps(bool setActiveColor)
        {
            var validStamps = StampManager.GetTableStamps().ToArray();
            activeStamps.RemoveAll(c =>
            {
                if(!validStamps.Any(s => s.ProductId == c.prodId))
                {
                    c.backRenderer.color = Color.clear;
                    return true;
                }
                else if(setActiveColor)
                {
                    c.backRenderer.color = Color.gray;
                }
                return false;
            });
        }
        var groups = MoreCosmic.AllStamps.Values.Where(s => !(s.IsHidden ?? false)).GroupBy(s => s.Package);

        var firstTableCache = StampManager.GetTableStamps().ToArray();
        foreach (var package in MoreCosmic.AllPackages.OrderBy(p => p.Value.Priority))
        {
            if(groups.Find(g => g.Key == package.Key, out var group))
            {
                if (widgets.Count > 0) widgets.Add(GUI.API.VerticalMargin(0.15f));

                widgets.Add(
                    GUI.API.VerticalHolder(GUIAlignment.Center,
                        GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentBold), package.Value.DisplayName),
                    GUI.API.Arrange(GUIAlignment.Center,
                    group.Select(s =>
                    {
                        SpriteRenderer backRenderer = null!;
                        return new NoSGUIFramed(GUIAlignment.Center, s.GetStampWidget(s.ProductId, PlayerControl.LocalPlayer.PlayerId, GUIAlignment.Center, true, 0.6f, _ => {
                            var currentTable = StampManager.CurrentTable;
                            bool isActive = currentTable.Contains(s.ProductId);

                            if (isActive)
                            {
                                //削除
                                if (selectedTableStamp == s.ProductId) selectedTableStamp = null;
                                StampManager.RemoveFromTable(s.ProductId);
                                UpdateUpperScreen();
                                UpdateActiveStamps(false);
                                backRenderer.color = Color.green;
                            }else if(currentTable.Length >= StampManager.MaxStamps && selectedTableStamp == null)
                            { 
                                //更に上画面で選択の必要があるとき
                                if (selectedInList?.prodId == s.ProductId)
                                    selectedInList = null;
                                else
                                {
                                    var text = Language.Translate("ui.stamp.full");
                                    DebugScreen.Push(new FunctionalDebugTextContent(() => text, FunctionalLifespan.GetTimeLifespan(3f)));

                                    if (selectedInList?.backRenderer)
                                    {
                                        selectedInList!.Value.backRenderer.color = activeStamps.Any(s => s.prodId == selectedInList?.prodId) ? Color.grey : Color.clear;
                                    }
                                    selectedInList = (s.ProductId, backRenderer);
                                }
                            }
                            else
                            {
                                activeStamps.Add((s.ProductId, backRenderer));
                                if (selectedTableStamp != null)
                                {
                                    StampManager.SetToTable(selectedTableStamp, s.ProductId);
                                    selectedTableStamp = null;
                                }
                                else
                                    StampManager.AddToTable(s.ProductId);
                                UpdateUpperScreen();
                                UpdateActiveStamps(false);
                            }
                            NebulaManager.Instance.HideHelpWidget();
                        }, () => s.GetStampLabelWidget(s.ProductId, Language.Translate(StampManager.CurrentTable.Contains(s.ProductId) ? "ui.stamp.click.bottom.2" : "ui.stamp.click.bottom.1")),
                        () =>
                        {
                            //カーソルのある要素
                            backRenderer.color = Color.green;
                        },
                        () =>
                        {
                            //カーソルが外された要素
                            backRenderer.color = selectedInList?.prodId == s.ProductId ? selectedColor : activeStamps.Any(a => a.prodId == s.ProductId) ? Color.gray : Color.clear;
                        }
                        ), new Vector2(0.05f, 0.05f), Color.clear)
                        { 
                            PostBuilder = renderer =>
                            {
                                backRenderer = renderer;
                                backRenderer.gameObject.AddComponent<SortingGroup>().sortingOrder = -1;
                                backRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                if (firstTableCache.Any(ftc => ftc.ProductId == s.ProductId)) {
                                    activeStamps.Add((s.ProductId, backRenderer));
                                    backRenderer.color = Color.gray;
                                }
                            }
                        };
                    }), 9)
                    )
                );
            }
        }

        return new MetaWidgetOld.WrappedWidget(
            new VerticalWidgetsHolder(GUIAlignment.Center,
                GUI.API.HorizontalMargin(7.4f),
                GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBold), "ui.stamp.onTable"),
                upperView,
                GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBold), "ui.stamp.all"),
                new GUIScrollView(GUIAlignment.Center, new(7.4f, HelpHeight - 1.5f), 
                    GUI.API.VerticalHolder(GUIAlignment.Center, widgets)
                )
            )
         );
    }
}

public class HintManager
{ 
    internal static List<Virial.Media.Hint> AllHints = [];

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
        var animRenderer = anim.GetComponent<SpriteRenderer>();
        AmongUsUtil.SetPlayerMaterial(animRenderer, DynamicPalette.MyColor.MainColor, DynamicPalette.MyColor.ShadowColor, DynamicPalette.MyVisorColor.ToColor(Palette.VisorColor));
        animRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
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


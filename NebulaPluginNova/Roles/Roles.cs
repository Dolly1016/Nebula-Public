using NAudio.CoreAudioApi;

using Nebula.Modules.GUIWidget;
using Nebula.Scripts;
using System.Reflection;
using System.Runtime.InteropServices;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;
using static Nebula.Configuration.ConfigurationValues;

namespace Nebula.Roles;

[NebulaPreprocess(PreprocessPhase.PreRoles)]
internal class NoSRoleSetUp
{
    static private void SetTabs()
    {
        //Set Up Tabs
        ConfigurationTab.tabSetting = new ConfigurationTab(0x01, "options.tab.setting", new(0.75f, 0.75f, 0.75f));
        ConfigurationTab.tabCrewmate = new ConfigurationTab(0x02, "options.tab.crewmate", new(Palette.CrewmateBlue));
        ConfigurationTab.tabImpostor = new ConfigurationTab(0x04, "options.tab.impostor", new(Palette.ImpostorRed));
        ConfigurationTab.tabNeutral = new ConfigurationTab(0x08, "options.tab.neutral", new(244f / 255f, 211f / 255f, 53f / 255f));
        ConfigurationTab.tabGhost = new ConfigurationTab(0x10, "options.tab.ghost", new(150f / 255f, 150f / 255f, 150f / 255f));
        ConfigurationTab.tabModifier = new ConfigurationTab(0x20, "options.tab.modifier", new(255f / 255f, 255f / 255f, 243f / 255f));
        ConfigurationTab.allTab = [ConfigurationTab.tabSetting, ConfigurationTab.tabCrewmate, ConfigurationTab.tabImpostor, ConfigurationTab.tabNeutral, ConfigurationTab.tabGhost, ConfigurationTab.tabModifier];
    }

    static private void SetNebulaTeams()
    {
        //Set Up Team
        Virial.Assignable.NebulaTeams.CrewmateTeam = Crewmate.Crewmate.MyTeam;
        Virial.Assignable.NebulaTeams.ImpostorTeam = Impostor.Impostor.MyTeam;
        Virial.Assignable.NebulaTeams.ArsonistTeam = Neutral.Arsonist.MyTeam;
        Virial.Assignable.NebulaTeams.ChainShifterTeam = Neutral.ChainShifter.MyTeam;
        Virial.Assignable.NebulaTeams.JackalTeam = Neutral.Jackal.MyTeam;
        Virial.Assignable.NebulaTeams.JesterTeam = Neutral.Jester.MyTeam;
        Virial.Assignable.NebulaTeams.PaparazzoTeam = Neutral.Paparazzo.MyTeam;
        Virial.Assignable.NebulaTeams.VultureTeam = Neutral.Vulture.MyTeam;
    }

    static NoSRoleSetUp()
    {
        SetTabs();

        SetNebulaTeams();
        foreach (var assembly in AddonScriptManager.ScriptAssemblies.Where(script => script.Behaviour.LoadRoles).Select(s => s.Assembly).Prepend(Assembly.GetAssembly(typeof(Roles))))
        {
            var types = assembly?.GetTypes().Where((type) => type.IsAssignableTo(typeof(DefinedAssignable)) || type.IsAssignableTo(typeof(PerkFunctionalInstance)));
            foreach(var type in types ?? []) System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }
}

[NebulaPreprocess(PreprocessPhase.FixRoles)]
public class Roles
{
    static public IReadOnlyList<DefinedRole> AllRoles { get; private set; } = [];
    static public IReadOnlyList<DefinedModifier> AllModifiers { get; private set; } = [];
    static public IReadOnlyList<DefinedGhostRole> AllGhostRoles { get; private set; } = [];

    static public DefinedRole? GetRole(int id)
    {
        if (id < 0) return null;
        return AllRoles[id];
    }

    static public DefinedModifier? GetModifier(int id)
    {
        if (id < 0) return null;
        return AllModifiers[id];
    }

    static public DefinedGhostRole? GetGhostRole(int id)
    {
        if (id < 0) return null;
        return AllGhostRoles[id];
    }

    static public IEnumerable<DefinedAssignable> AllAssignables()
    {
        foreach(var r in AllRoles) yield return r;
        foreach (var r in AllGhostRoles) yield return r;
        foreach (var m in AllModifiers) yield return m;
    }

    static public IEnumerable<DefinedAllocatableModifier> AllAllocatableModifiers()
    {
        foreach (var m in AllModifiers) if (m is DefinedAllocatableModifier adm) yield return adm;
    }

    static public IReadOnlyList<Team> AllTeams { get; private set; } = null!;

    static private List<DefinedRole>? allRoles = [];
    static private List<DefinedGhostRole>? allGhostRoles = [];
    static private List<DefinedModifier>? allModifiers = [];
    static private List<Team>? allTeams = [];

    static private Dictionary<string, PerkFunctionalDefinition> allPerks = [];
    static internal IEnumerable<PerkFunctionalDefinition> AllPerks => allPerks.Values;
    static internal void Register(PerkFunctionalDefinition definition) => allPerks[definition.Id] = definition;
    static internal PerkFunctionalDefinition GetPerk(string id) => allPerks[id];

    static public void CheckNeedingHandshake(object assignable)
    {
        var assembly = assignable.GetType().Assembly;
        var addon = AddonScriptManager.ScriptAssemblies.FirstOrDefault(addon => assembly == addon.Assembly);
        if (addon != null)
        {
            addon.Addon.MarkAsNeedingHandshake();
        }
    }

    static public void Register(DefinedRole role)
    {
        if (allRoles == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register role \"{role.LocalizedName}\".\nRole registration is only possible at load phase.");
        else
            allRoles?.Add(role);

        CheckNeedingHandshake(role);
    }

    static public void Register(DefinedGhostRole role)
    {
        if (allRoles == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register role \"{role.LocalizedName}\".\nRole registration is only possible at load phase.");
        else
            allGhostRoles?.Add(role);

        CheckNeedingHandshake(role);
    }

    static public void Register(DefinedModifier role)
    {
        if(allModifiers == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register modifier \"{role.LocalizedName}\".\nModifier registration is only possible at load phase.");
        else
            allModifiers?.Add(role);

        CheckNeedingHandshake(role);
    }
    static public void Register(Team team) {
        if(allTeams == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register team \"{team.TranslationKey}\".\nTeam registration is only possible at load phase.");
        else
            allTeams.Add(team);

        CheckNeedingHandshake(team);
    }

    static public IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Building Roles Database");

        allRoles!.Sort((role1, role2) => {
            int diff;
            
            diff = (int)role1.Category - (int)role2.Category;
            if (diff != 0) return diff;

            return role1.InternalName.CompareTo(role2.InternalName);
        });

        allGhostRoles!.Sort((role1, role2) => {
            int diff = (int)role1.Category - (int)role2.Category;
            if (diff != 0) return diff;
            return role1.InternalName.CompareTo(role2.InternalName);
        });


        allModifiers!.Sort((role1, role2) => {
            return role1.InternalName.CompareTo(role2.InternalName);
        });

        allTeams!.Sort((team1, team2) => team1.TranslationKey.CompareTo(team2.TranslationKey));

        for (int i = 0; i < allRoles!.Count; i++) allRoles![i].Id = i;
        for (int i = 0; i < allGhostRoles!.Count; i++) allGhostRoles![i].Id = i;
        for (int i = 0; i < allModifiers!.Count; i++) allModifiers![i].Id = i;

        AllRoles = allRoles!.AsReadOnly();
        AllGhostRoles = allGhostRoles!.AsReadOnly();
        AllModifiers = allModifiers!.AsReadOnly();
        AllTeams = allTeams!.AsReadOnly();

        foreach (var role in AllRoles)
        {
            if (role.ConfigurationHolder != null && role.AllocationParameters != null)
            {
                if(role.ModifierFilter != null) role.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.modifierFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen<DefinedModifier>("ModifierFilter", AllAllocatableModifiers().Where(m => role.CanLoadDefault(m)), m => role.ModifierFilter));
                if (role.GhostRoleFilter != null) role.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.ghostRoleFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("GhostRoleFilter", AllGhostRoles.Where(m => role.CanLoadDefault(m)), g => role.GhostRoleFilter));
            }
        }

        foreach(var modifier in AllAllocatableModifiers())
            if (modifier.ConfigurationHolder != null)
                modifier.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.modifierFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("RoleFilter", AllRoles.Where(r => r.CanLoadDefault(modifier)), r => modifier.RoleFilter));

        foreach (var ghostRole in AllGhostRoles)
            if (ghostRole.ConfigurationHolder != null)
                ghostRole.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.ghostRoleFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("RoleFilter", AllRoles.Where(r => r.CanLoadDefault(ghostRole)), r => ghostRole.RoleFilter));

        //AllAssignables().Do(a => a.Load());


        allRoles = null;
        allGhostRoles = null;
        allModifiers = null;
        allTeams = null;

        //色を登録する
        AllAssignables().Do(a => SerializableDocument.RegisterColor("role." + a.InternalName, a.Color.ToUnityColor()));
    }
}

internal static class RoleOptionHelper
{
    private static readonly TextAttribute RelatedOutsideButtonAttr = new(GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed)) { Size = new(1.2f, 0.29f) };
    private static readonly TextAttribute RelatedInsideButtonAttr = new(GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed)) { Size = new(1.14f, 0.26f), Font = GUI.API.GetFont(FontAsset.GothicMasked) };

    static internal void OpenFilterScreen<R>(string scrollerTag, IEnumerable<R> allRoles, Func<R, AssignableFilter<R>> filter, MetaScreen? screen = null) where R : DefinedAssignable
        => OpenFilterScreen(scrollerTag, allRoles, r => filter.Invoke(r).Test(r), (r, val) => filter.Invoke(r).SetAndShare(r, val), r => filter.Invoke(r).ToggleAndShare(r), screen);
    static internal void OpenFilterScreen<R>(string scrollerTag, IEnumerable<R> allRoles, Func<R, bool> test, Action<R,bool>? setAndShare, Action<R> toggleAndShare, MetaScreen? screen = null) where R : DefinedAssignable
    {
        if (!screen) screen = MetaScreen.GenerateWindow(new Vector2(6.7f, setAndShare != null ? 4.5f : 3.7f), HudManager.Instance.transform, Vector3.zero, true, true);

        bool showOnlySpawnable = ClientOption.AllOptions[ClientOption.ClientOptionType.ShowOnlySpawnableAssignableOnFilter].Value == 1;

        IEnumerable<R> allRolesFiltered = showOnlySpawnable ? allRoles.Where(r => (r as ISpawnable)?.IsSpawnable ?? true) : allRoles;

        List<GUIWidget> shortcutButtons = [];
        if(setAndShare != null)
        {
            void Append(string translationKey, Func<bool> isInvalid, Action<bool> onClicked)
            {
                var invalid = isInvalid.Invoke();
                shortcutButtons.Add(new GUIButton(Virial.Media.GUIAlignment.Center, RelatedOutsideButtonAttr, GUI.API.LocalizedTextComponent(translationKey))
                {
                    Color = invalid ? Color.gray : Color.white,
                    OnClick = _ =>
                    {
                        {
                            //データのセーブと共有を一括で行う
                            using var segment = new DataSaveSegment();
                            using var shareSegment = new ConfigurationUpdateBlocker();
                            onClicked.Invoke(invalid);
                        }
                        OpenFilterScreen(scrollerTag, allRoles, test, setAndShare, toggleAndShare, screen);
                    },
                    AsMaskedButton = false
                });
            }

            Append("roleFilter.shortcut.all", () => allRolesFiltered.Any(r => !test.Invoke(r)), val => allRolesFiltered.Do(r => setAndShare.Invoke(r, val)));

            if (typeof(R).IsAssignableTo(typeof(DefinedRole)))
            {
                if (allRolesFiltered.Any(r => (r as DefinedRole)!.Category == RoleCategory.ImpostorRole))
                {
                    var impostors = allRolesFiltered.Where(r => (r as DefinedRole)!.Category == RoleCategory.ImpostorRole);
                    Append("roleFilter.shortcut.allImpostor", () => impostors.Any(r => !test.Invoke(r)), val => impostors.Do(r => setAndShare.Invoke(r, val)));
                }
                if (allRolesFiltered.Any(r => (r as DefinedRole)!.Category == RoleCategory.NeutralRole))
                {
                    var neutrals = allRolesFiltered.Where(r => (r as DefinedRole)!.Category == RoleCategory.NeutralRole);
                    Append("roleFilter.shortcut.allNeutral", () => neutrals.Any(r => !test.Invoke(r)), val => neutrals.Do(r => setAndShare.Invoke(r, val)));
                }
                if (allRolesFiltered.Any(r => (r as DefinedRole)!.Category == RoleCategory.CrewmateRole))
                {
                    var crewmates = allRolesFiltered.Where(r => (r as DefinedRole)!.Category == RoleCategory.CrewmateRole);
                    Append("roleFilter.shortcut.allCrewmate", () => crewmates.Any(r => !test.Invoke(r)), val => crewmates.Do(r => setAndShare.Invoke(r, val)));
                }
            }
        }

        screen!.SetWidget(new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center,
            new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Center, shortcutButtons),
            new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Center, new NoSGUICheckbox(Virial.Media.GUIAlignment.Center, showOnlySpawnable) { OnValueChanged = val =>
            {
                ClientOption.AllOptions[ClientOption.ClientOptionType.ShowOnlySpawnableAssignableOnFilter].Increment();
                OpenFilterScreen(scrollerTag, allRoles, test, setAndShare, toggleAndShare, screen);
            } }, GUI.API.HorizontalMargin(0.2f), GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayContent), "roleFilter.showOnlySpawnable")),
            new GUIScrollView(Virial.Media.GUIAlignment.Center, new(6.5f, 3.1f), GUI.API.Arrange(Virial.Media.GUIAlignment.Center,
            allRolesFiltered.Select(r => new GUIButton(Virial.Media.GUIAlignment.Center, RelatedInsideButtonAttr, GUI.API.RawTextComponent(r.DisplayColoredName)) { 
                OnClick = _ => { toggleAndShare(r); OpenFilterScreen(scrollerTag, allRoles, test, setAndShare, toggleAndShare, screen); },
                Color = test(r) ? Color.white : new Color(0.14f, 0.14f, 0.14f),
                AsMaskedButton = true,
            })
            , 4)) { ScrollerTag = scrollerTag, WithMask = true}), out _);
    }
}
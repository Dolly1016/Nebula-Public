using NAudio.CoreAudioApi;
using Nebula.Compat;
using System.Reflection;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Runtime;

namespace Nebula.Roles;

[NebulaPreprocessForNoS(PreprocessPhaseForNoS.PreRoles)]
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

        var types = Assembly.GetAssembly(typeof(Roles))?.GetTypes().Where((type) => type.IsAssignableTo(typeof(DefinedAssignable)));
        if (types == null) return;

        foreach (var type in types)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

        SetNebulaTeams();
    }
}

[NebulaPreprocessForNoS(PreprocessPhaseForNoS.FixRoles)]
public class Roles
{
    static public IReadOnlyList<DefinedRole> AllRoles { get; private set; } = null!;
    static public IReadOnlyList<DefinedModifier> AllModifiers { get; private set; } = null!;
    static public IReadOnlyList<DefinedGhostRole> AllGhostRoles { get; private set; } = null!;

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

    static private List<DefinedRole>? allRoles = new();
    static private List<DefinedGhostRole>? allGhostRoles = new();
    static private List<DefinedModifier>? allModifiers = new();
    static private List<Team>? allTeams = new();

    static public void Register(DefinedRole role) {
        if(allRoles == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register role \"{role.LocalizedName}\".\nRole registration is only possible at load phase.");
        else
            allRoles?.Add(role);
    }
    static public void Register(DefinedGhostRole role)
    {
        if (allRoles == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register role \"{role.LocalizedName}\".\nRole registration is only possible at load phase.");
        else
            allGhostRoles?.Add(role);
    }
    static public void Register(DefinedModifier role)
    {
        if(allModifiers == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register modifier \"{role.LocalizedName}\".\nModifier registration is only possible at load phase.");
        else
            allModifiers?.Add(role);
    }
    static public void Register(Team team) {
        if(allTeams == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register team \"{team.TranslationKey}\".\nTeam registration is only possible at load phase.");
        else
            allTeams.Add(team);
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
                if(role.ModifierFilter != null) role.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.modifierFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("ModifierFilter", AllAllocatableModifiers().Where(m => role.CanLoadDefault(m)), m => role.ModifierFilter!.Test(m), m => role.ModifierFilter.ToggleAndShare(m)));
                if (role.GhostRoleFilter != null) role.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.ghostRoleFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("GhostRoleFilter", AllGhostRoles.Where(m => role.CanLoadDefault(m)), g => role.GhostRoleFilter!.Test(g), g => role.GhostRoleFilter.ToggleAndShare(g)));
            }
        }

        foreach(var modifier in AllAllocatableModifiers())
            if (modifier.ConfigurationHolder != null)
                modifier.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.modifierFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("RoleFilter", AllRoles.Where(r => r.CanLoadDefault(modifier)), r => (r.ModifierFilter?.Test(modifier) ?? false), r => r.ModifierFilter?.ToggleAndShare(modifier)));

        foreach (var ghostRole in AllGhostRoles)
            if (ghostRole.ConfigurationHolder != null)
                ghostRole.ConfigurationHolder.AppendRelatedAction(new TranslateTextComponent("options.role.ghostRoleFilter"), () => true, () => RoleOptionHelper.OpenFilterScreen("RoleFilter", AllRoles.Where(r => r.CanLoadDefault(ghostRole)), r => (r.GhostRoleFilter?.Test(ghostRole) ?? false), r => r.GhostRoleFilter?.ToggleAndShare(ghostRole)));

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
    private static TextAttributeOld RelatedInsideButtonAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.1f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };

    static internal void OpenFilterScreen<R>(string scrollerTag, IEnumerable<R> allRoles, Func<R,bool> test, Action<R> toggleAndShare, MetaScreen? screen = null) where R : DefinedAssignable
    {
        if (!screen) screen = MetaScreen.GenerateWindow(new Vector2(5f, 3.2f), HudManager.Instance.transform, Vector3.zero, true, true);

        MetaWidgetOld inner = new();
        inner.Append(allRoles, (r) => new MetaWidgetOld.Button(() => { toggleAndShare(r); OpenFilterScreen(scrollerTag, allRoles, test, toggleAndShare, screen); }, RelatedInsideButtonAttr)
        {
            RawText = r.DisplayColordName,
            PostBuilder = (button, renderer, text) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask,
            Alignment = IMetaWidgetOld.AlignmentOption.Center,
            Color = test(r) ? Color.white : new Color(0.14f, 0.14f, 0.14f)
        }, 3, -1, 0, 0.6f);

        screen!.SetWidget(new MetaWidgetOld.ScrollView(new(5f, 3.1f), inner, true) { ScrollerTag = scrollerTag });
    }
}
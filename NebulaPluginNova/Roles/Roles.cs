using Nebula.Compat;
using System.Reflection;
using Virial.Assignable;

namespace Nebula.Roles;

[NebulaPreLoad(typeof(RemoteProcessBase),typeof(Team),typeof(NebulaAddon))]
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

    static public IEnumerable<IntroAssignableModifier> AllIntroAssignableModifiers()
    {
        foreach (var m in AllModifiers) if (m is IntroAssignableModifier iam) yield return iam;
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

    static public IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Building Roles Database";
        yield return null;

        var iroleType = typeof(AbstractRole);
        var types = Assembly.GetAssembly(typeof(AbstractRole))?.GetTypes().Where((type) => type.IsAssignableTo(typeof(IAssignableBase)) || type.IsAssignableTo(typeof(PerkInstance)) || type.IsDefined(typeof(NebulaRoleHolder)));
        if (types == null) yield break;

        foreach (var type in types)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

        SetNebulaTeams();

        allRoles!.Sort((role1, role2) => {
            int diff;
            
            diff = (int)role1.Category - (int)role2.Category;
            if (diff != 0) return diff;

            diff = (role1.IsDefaultRole ? -1 : 1) - (role2.IsDefaultRole ? -1 : 1);
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

        AllAssignables().Do(a => a.Load());
        

        allRoles = null;
        allGhostRoles = null;
        allModifiers = null;
        allTeams = null;

        //色を登録する
        AllAssignables().Do(a => SerializableDocument.RegisterColor("role." + a.InternalName, a.Color.ToUnityColor()));
    }
}

using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class DamnedImpostor : DefinedRoleTemplate, DefinedRole
{
    static public readonly DamnedImpostor MyRole = new();

    private DamnedImpostor():base("damnedImpostor", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, null, false, false) { }
    

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    bool DefinedAssignable.ShowOnHelpScreen => false;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => [Modifier.Damned.MyRole];
        public Instance(GamePlayer player) : base(player){}

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) new StaticAchievementToken("damned.common2");
        }
    }
}

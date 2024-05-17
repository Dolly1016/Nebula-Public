using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class DamnedImpostor : DefinedRoleTemplate, DefinedRole
{
    static public DamnedImpostor MyRole = new DamnedImpostor();

    private DamnedImpostor():base("damned", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, null, false, false) { }
    

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    bool DefinedAssignable.ShowOnHelpScreen => false;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player){}

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) new StaticAchievementToken("damned.common2");
        }
    }
}

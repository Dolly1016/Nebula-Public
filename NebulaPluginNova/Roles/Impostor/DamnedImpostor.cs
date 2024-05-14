using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class DamnedImpostor : AbstractRole, DefinedRole
{
    static public DamnedImpostor MyRole = new DamnedImpostor();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    string DefinedAssignable.LocalizedName => "damned";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override int RoleCount => 0;
    public override float GetRoleChance(int count) => 0f;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public override void Load(){}
    bool DefinedAssignable.ShowOnHelpScreen => false;

    public class Instance : Impostor.Instance, RuntimeRole
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player){}

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) new StaticAchievementToken("damned.common2");
        }
    }
}

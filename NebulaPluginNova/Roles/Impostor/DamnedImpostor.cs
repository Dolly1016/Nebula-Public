using Virial.Assignable;

namespace Nebula.Roles.Impostor;

public class DamnedImpostor : AbstractRole, DefinedAssignable
{
    static public DamnedImpostor MyRole = new DamnedImpostor();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "damned";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override int RoleCount => 0;
    public override float GetRoleChance(int count) => 0f;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public override void Load(){}
    bool DefinedAssignable.ShowOnHelpScreen => false;

    public class Instance : Impostor.Instance
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player){}

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner) new StaticAchievementToken("damned.common2");
        }
    }
}

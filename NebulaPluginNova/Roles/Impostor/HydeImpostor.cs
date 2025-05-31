using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Impostor;

internal class HydeImpostor : DefinedRoleTemplate, DefinedRole
{
    static public readonly HydeImpostor MyRole = new();

    private HydeImpostor() : base("hydeImpostor", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, null, false, false) { }


    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    bool DefinedAssignable.ShowOnHelpScreen => false;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => [Crewmate.JekyllAndHyde.MyRole];
        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated()
        {
        }
    }
}

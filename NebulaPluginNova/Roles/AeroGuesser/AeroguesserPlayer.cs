using Nebula.Roles.Complex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;

namespace Nebula.Roles.AeroGuesser;

internal class AeroguesserPlayer : DefinedRoleTemplate, DefinedRole
{

    private AeroguesserPlayer() : base("aeroguesser.player", new(Palette.CrewmateBlue), RoleCategory.CrewmateRole, Crewmate.Crewmate.MyTeam, withOptionHolder: false)
    {
    }

    static public readonly AeroguesserPlayer MyRole = new();

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    bool ISpawnable.IsSpawnable => false;
    bool DefinedAssignable.ShowOnHelpScreen => false;
    bool DefinedAssignable.ShowOnFreeplayScreen => false;
    bool DefinedAssignable.WithStatistics => false;
    bool DefinedRole.IsSystemRole => true;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => [];
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated() { }
    }
}

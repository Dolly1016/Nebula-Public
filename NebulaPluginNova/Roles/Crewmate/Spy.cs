using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Game;

namespace Nebula.Roles.Crewmate;
/*
internal class Spy : DefinedRoleTemplate, HasCitation, DefinedRole
{
    private Spy() : base("spy", new(Palette.ImpostorRed), RoleCategory.CrewmateRole, Crewmate.MyTeam, [])
    {
    }

    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.VentConfiguration("role.spy.vent", false, null, 0, (0f, 30f, 2.5f), 0f, (0f, 20f, 2.5f), 10f);

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static public readonly Spy MyRole = new();

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;
        bool RuntimeRole.CanUseVent => true;
        public Instance(GamePlayer player, int[] argument) : base(player, VentConfiguration)
        {
        }
        public override void OnActivated()
        {   
        }
        int[]? RuntimeAssignable.RoleArguments => null;
    }
}

*/

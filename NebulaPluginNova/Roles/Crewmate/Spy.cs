using Nebula.Roles.Complex;
using Nebula.Roles.Impostor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using static Nebula.Roles.Impostor.Creeping;

namespace Nebula.Roles.Crewmate;

internal class Spy : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument, ISpawnable
{
    private Spy() : base("spy", new(Palette.ImpostorRed), RoleCategory.CrewmateRole, Crewmate.MyTeam, [VentConfiguration, NumOfExemptedTasksOption])
    {
    }

    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.VentConfiguration("role.spy.vent", false, null, 0, (0f, 30f, 2.5f), 0f, (0f, 20f, 2.5f), 10f);
    static private readonly IntegerConfiguration NumOfExemptedTasksOption = NebulaAPI.Configurations.Configuration("options.role.spy.numOfExemptedTasks", (1, 8), 3);

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static public readonly Spy MyRole = new();

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    bool DefinedRole.IsImpostorlike => true;
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable assignable) => CanLoadDefaultTemplate(assignable) && assignable != ShownSecret.OptionRole;

    bool ISpawnable.CanSpawnIn(GameParameters game)
    {
        return game.Impostors > 1;
    }
    
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
        

        [EventPriority(-100)]
        void OnIntro(GameShowIntroLocalEvent ev)
        {
            if (ev.RelatedTeam == Impostor.Impostor.MyTeam) ev.AddPlayer(MyPlayer);
        }

        [OnlyMyPlayer]
        void OnSetTaskLocal(PlayerTasksTrySetLocalEvent ev)
        {
            int extempts = NumOfExemptedTasksOption;
            for (int i = 0; i < extempts; i++)
            {
                if (ev.Tasks.Count == 0) break;
                ev.Tasks.RemoveAt(System.Random.Shared.Next(ev.Tasks.Count));
            }
        }
    }
}
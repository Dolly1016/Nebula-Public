using Nebula.Roles.Complex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial;

namespace Nebula.Roles.Neutral;

public class Ember : DefinedRoleTemplate, DefinedRole
{
    private Ember() : base("ember", new(99, 80, 80), RoleCategory.NeutralRole, ChainShifter.MyTeam, null, false, true, () => false) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public Ember MyRole = new Ember();
    bool DefinedAssignable.ShowOnHelpScreen => false;
    bool DefinedAssignable.ShowOnFreeplayScreen => false;
    bool IGuessed.CanBeGuessDefault => false;
    bool AssignableFilterHolder.CanLoadDefault(DefinedAssignable _) => false;


    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated(){}

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.Winners.Test(MyPlayer))
                new StaticAchievementToken("ember.another2");
            else
                new StaticAchievementToken("ember.another1");
        }
    }
}


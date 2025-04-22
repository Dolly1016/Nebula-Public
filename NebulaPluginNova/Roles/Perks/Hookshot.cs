using Nebula.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class Hookshot : PerkFunctionalInstance
{
    static PerkFunctionalDefinition Def = new("hookshot", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("hookshot", 8, 57, (Crewmate.Climber.MyRole as DefinedAssignable).Color), (def, instance) => new Hookshot(def, instance));

    bool used = false;
    public Hookshot(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (used) return;

        Climber.SearchPointAndSendJump();
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(used ? Color.gray : Color.white);
    }
}

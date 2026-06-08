using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class KetchupMessage : PerkFunctionalInstance
{
    static PerkFunctionalDefinition Def = new("ketchupMessage", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("ketchupMessage", 6, 60, new(63, 43, 69), Virial.Color.ImpostorColor), (def, instance) => new KetchupMessage(def, instance));

    bool used = false;
    public KetchupMessage(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
    }


    public override bool HasAction => true;
    public override void OnClick()
    {
        if (used) return;

        var canvas = DyingMessages.GenerateCanvas(GamePlayer.LocalPlayer!.Position, Modifier.DyingMessage.MessageDuration, null);
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor((used) ? Color.gray : Color.white);
    }
}

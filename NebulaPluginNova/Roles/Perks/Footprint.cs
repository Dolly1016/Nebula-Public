using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class Footprint : PerkFunctionalInstance, IGameOperator
{
    static PerkFunctionalDefinition Def = new("footprint", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("footprint", 3, 29, new(36, 224, 174)), (def, instance) => new Footprint(def, instance));

    private Footprint(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        GamePlayer.LocalPlayer?.GainAttribute(PlayerAttributes.Footprint, 100000f, true, 0, EffectTag);
    }

    private const string EffectTag = "footprint";
    void IGameOperator.OnReleased()
    {
        PlayerModInfo.RpcRemoveAttrByTag.Invoke((GamePlayer.LocalPlayer!.PlayerId, EffectTag));
    }
}
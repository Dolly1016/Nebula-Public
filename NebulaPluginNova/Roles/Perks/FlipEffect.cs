using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class FlipEffect : PerkFunctionalInstance, IGameOperator
{
    static private int DuplicateCounter = 0;
    private int MyDuplicateCounter { get; init; }
    static PerkFunctionalDefinition DefX = new("flipX", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("flipX", 5, 44, new(154, 133, 157)), (def, instance) => new FlipEffect(def, instance, PlayerAttributes.FlipX));
    static PerkFunctionalDefinition DefY = new("flipY", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("flipY", 5, 45, new(17,217,17)), (def, instance) => new FlipEffect(def, instance, PlayerAttributes.FlipY));
    static PerkFunctionalDefinition DefXY = new("flipXY", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("flipXY", 5, 46, new(156, 71,48)), (def, instance) => new FlipEffect(def, instance, PlayerAttributes.FlipXY));

    private FlipEffect(PerkDefinition def, PerkInstance instance, IPlayerAttribute flip) : base(def, instance)
    {
        MyDuplicateCounter = DuplicateCounter++;

        Vector2 normVec = Vector2.one;
        if (flip == PlayerAttributes.FlipX)
            normVec = new(-1f, 1f);
        else if (flip == PlayerAttributes.FlipY)
            normVec = new(1f, -1f);
        else 
            normVec = new(-1f, -1f);

        using (RPCRouter.CreateSection("FlipEffect"))
        {
            NebulaGameManager.Instance?.AllPlayerInfo.Do(p => p.GainAttribute(flip, 100000f, true, 0, EffectTag));
            NebulaGameManager.Instance?.AllPlayerInfo.Do(p => PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new SpeedModulator(1f, normVec, true, 100000f, true, 0, EffectTag), true)));
        }

        new StaticAchievementToken("perk.manyPerks1" + def.localizedName);
        if(MyPlayer.IsCrewmate) new StaticAchievementToken("perk.screen");
    }

    private string EffectTag => "flipXEffect" + MyPlayer.PlayerId + "-" + MyDuplicateCounter;
    void IGameOperator.OnReleased()
    {
        using (RPCRouter.CreateSection("FlipEffect"))
        {
            NebulaGameManager.Instance?.AllPlayerInfo.Do(p => PlayerModInfo.RpcRemoveAttrByTag.Invoke((p.PlayerId, EffectTag)));
        }
    }
}
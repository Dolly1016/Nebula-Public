using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Components;
using Virial.Events.Game;
using Virial.Game;
using static Sentry.MeasurementUnit;

namespace Nebula.Roles.Perks;

internal class EraseBody : PerkFunctionalInstance
{
    static internal PerkFunctionalDefinition Def = new("eraseBody", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("eraseBody", 1, 10019, new(150, 92, 150)), (def, instance) => new EraseBody(def, instance));
    bool used = false;
    ObjectTracker<Virial.Game.DeadBody> tracker;
    private EraseBody(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        tracker = ObjectTrackers.ForDeadBody(this, null, MyPlayer, (d) => true);
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (used) return;
        if (tracker.CurrentTarget == null) return;

        AmongUsUtil.RpcCleanDeadBody(tracker.CurrentTarget!, MyPlayer.PlayerId, EventDetail.Clean);
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor((used || tracker.CurrentTarget == null) ? Color.gray : Color.white);
    }
}
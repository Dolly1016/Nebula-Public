using Il2CppSystem.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using static Sentry.MeasurementUnit;

namespace Nebula.Roles.Perks;

internal class PlaceTrap : PerkFunctionalInstance
{
    private bool used = false;
    private NebulaSyncObjectReference? obj = null;
    private Vector2 pos = Vector2.zero;
    private bool isAccel;
    static PerkFunctionalDefinition AccelTrap = new("accelTrap", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("accelTrap", 5, 41, new(65, 122, 186)), (def, instance) => new PlaceTrap(def, instance, true));
    static PerkFunctionalDefinition DecelTrap = new("decelTrap", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("decelTrap", 5, 42, new(184, 45, 51), Virial.Color.ImpostorColor), (def, instance) => new PlaceTrap(def, instance, false));
    

    private PlaceTrap(PerkDefinition def, PerkInstance instance, bool isAccel) : base(def, instance)
    {
        this.isAccel = isAccel;
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (used || MyPlayer.IsDead) return;

        Vector2 pos = (Vector2)MyPlayer.Position + new Vector2(0f, 0.085f);
        this.pos = pos;
        obj = NebulaSyncObject.LocalInstantiate(Complex.Trapper.Trap.MyLocalTag, [isAccel ? 0 : 1, pos.x, pos.y]);
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(used ? Color.gray : Color.white);
    }

    void OnUpdate(MeetingEndEvent ev)
    {
        if(obj != null)
        {
            NebulaSyncObject.RpcInstantiate(Complex.Trapper.Trap.MyGlobalTag, [isAccel ? 0 : 1, pos.x, pos.y]);
            NebulaSyncObject.LocalDestroy(obj.ObjectId);
        }
    }
}

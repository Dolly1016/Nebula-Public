using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Abilities;

public class DeadbodyArrowAbility : FlexibleLifespan, IGameOperator
{
    private List<(DeadBody deadBody, Arrow arrow)> AllArrows = new();
    public bool ShowArrow { get; set; } = true;

    void OnDeadBodyGenerated(DeadBodyInstantiateEvent ev)
    {
        AllArrows.Add((ev.DeadBody, new Arrow(null) { TargetPos = ev.DeadBody.TruePosition }.SetColor(Color.blue).Register(this)));
        LocalUpdate(null!);
    }

    void LocalUpdate(GameUpdateEvent ev)
    {
        AllArrows.RemoveAll((tuple) =>
        {
            tuple.arrow.IsActive = ShowArrow;

            if (tuple.deadBody)
            {
                tuple.arrow.TargetPos = tuple.deadBody.TruePosition;
                return false;
            }
            else
            {
                tuple.arrow.Release();
                return true;
            }
        });
    }
}

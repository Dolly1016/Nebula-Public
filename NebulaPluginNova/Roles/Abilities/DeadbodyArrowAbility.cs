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
    private List<(Virial.Game.DeadBody deadBody, Arrow arrow)> AllArrows = new();
    public bool ShowArrow { get; set; } = true;
    public Func<Virial.Game.DeadBody, bool>? Predicate { get; set; } = null;

    void OnDeadBodyGenerated(DeadBodyInstantiateEvent ev)
    {
        AllArrows.Add((ev.DeadBody, new Arrow(null) { TargetPos = ev.DeadBody.VanillaDeadBody.TruePosition }.SetColor(VColor.Blue).Register(this)));
        LocalUpdate(null!);
    }

    void LocalUpdate(GameUpdateEvent ev)
    {
        AllArrows.RemoveAll((tuple) =>
        {
            if (tuple.deadBody.VanillaDeadBody.AsBoolFast())
            {
                tuple.arrow.IsActive = ShowArrow && (Predicate?.Invoke(tuple.deadBody) ?? true);
                tuple.arrow.TargetPos = tuple.deadBody.TruePosition;
                return false;
            }
            else
            {
                tuple.arrow.IsActive = false;
                tuple.arrow.Release();
                return true;
            }
        });
    }
}

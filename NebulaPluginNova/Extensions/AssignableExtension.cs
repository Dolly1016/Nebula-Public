using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Extensions;

internal static class AssignableExtension
{
    public static void ActivateAssignable(this RuntimeAssignable assignable)
    {
        assignable.OnActivated();
        (assignable as IGameOperator)?.Register(assignable);
        if (assignable is RuntimeRole role)
        {
            GameOperatorManager.Instance?.Run(new PlayerRoleSetEvent(assignable.MyPlayer, role));
            assignable.MyPlayer.FeelBeTrueCrewmate = true;
        }
        else if (assignable is RuntimeModifier modifier)
            GameOperatorManager.Instance?.Run(new PlayerModifierSetEvent(assignable.MyPlayer, modifier));
    }
}

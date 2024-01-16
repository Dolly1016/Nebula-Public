using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles;

public abstract class ModifierInstance : AssignableInstance, RuntimeModifier
{
    public override IAssignableBase AssignableBase => Role;
    public abstract AbstractModifier Role { get; }
    DefinedModifier RuntimeModifier.Modifier => Role;
    Virial.Game.Player RuntimeAssignable.MyPlayer => MyPlayer;
    

    public ModifierInstance(PlayerModInfo player) : base(player)
    {
    }

    public virtual bool InvalidateCrewmateTask => false;
    public virtual string? IntroText => null;

}

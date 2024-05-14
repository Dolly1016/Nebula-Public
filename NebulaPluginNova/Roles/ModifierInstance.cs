using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles;

public abstract class ModifierInstance : AssignableInstance, RuntimeModifier
{
    public override IAssignableBase AssignableBase => Role;
    public abstract AbstractModifier Role { get; }
    DefinedModifier RuntimeModifier.Modifier => Role;
    Virial.Game.Player IBindPlayer.MyPlayer => MyPlayer;
    

    public ModifierInstance(GamePlayer player) : base(player)
    {
    }

    /// <summary>
    /// クルーメイトタスクを持っていた場合、目に見えるように無効化する
    /// </summary>
    public virtual bool InvalidateCrewmateTask => false;

    /// <summary>
    /// クルーメイトタスクを持っていたとしても、クルーメイトタスクの総数に計上されない場合はtrue
    /// </summary>
    public virtual bool MyCrewmateTaskIsIgnored => false;

    public virtual string? IntroText => null;

}

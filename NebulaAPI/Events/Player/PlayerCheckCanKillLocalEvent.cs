namespace Virial.Events.Player;

/// <summary>
/// キルできる相手かどうか調べます。
/// </summary>
public class PlayerCheckCanKillLocalEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Target { get; private init; }
    public bool CanKill { get
        {
            if (cannotKillForcedly) return false;
            if (canKillForcedly) return true;
            if (cannotKillBasically) return false;
            return true;
        } 
    }
    private bool cannotKillBasically = false;
    private bool canKillForcedly = false;
    private bool cannotKillForcedly = false;

    /// <summary>
    /// 基本的にキルできない相手に設定します。
    /// </summary>
    public void SetAsCannotKillBasically() => cannotKillBasically = true;
    /// <summary>
    /// 基本的な規則を無視してキルできる相手に設定します。
    /// </summary>
    public void SetAsCanKillForcedly() => canKillForcedly = true;
    /// <summary>
    /// 他の規則を無視してキルできない相手に設定します。
    /// </summary>
    public void SetAsCannotKillForcedly() => cannotKillForcedly = true;

    public PlayerCheckCanKillLocalEvent(Virial.Game.Player player, Virial.Game.Player target) : base(player)
    {
        this.Target = target;
    }
}

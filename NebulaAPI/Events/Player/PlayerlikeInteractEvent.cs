using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public record PlayerInteractParameter(bool RealPlayerOnly = false, bool IsKillInteraction = false, bool ResetCooldownEvenIfFailed = true);
public class PlayerInteractPlayerLocalEvent : AbstractPlayerEvent
{
    /// <summary>
    /// アクションを起こしたプレイヤー。
    /// </summary>
    public Virial.Game.Player User { get; set; }
    /// <summary>
    /// アクションを受けた対象。
    /// </summary>
    public Virial.Game.IPlayerlike Target { get; set; }
    /// <summary>
    /// インタラクションをキャンセルする場合、trueにしてください。
    /// </summary>
    public bool IsCanceled { get; set; } = false;
    /// <summary>
    /// インタラクションのパラメータ
    /// </summary>
    public PlayerInteractParameter Parameters { get; init; }

    internal PlayerInteractPlayerLocalEvent(Virial.Game.Player user, Virial.Game.IPlayerlike target, PlayerInteractParameter parameters) : base(user)
    {
        User = user;
        Target = target;
        Parameters = parameters;
        IsCanceled = target != target.RealPlayer && parameters.RealPlayerOnly;
    }
}

/// <summary>
/// 誰かが自身が所有する偽のプレイヤーに失敗したインタラクトをすると発火します。
/// </summary>
public class PlayerInteractionFailedForMyFakePlayerEvent : AbstractPlayerEvent
{
    /// <summary>
    /// アクションを起こしたプレイヤー。
    /// </summary>
    public Virial.Game.Player User { get; set; }
    /// <summary>
    /// アクションを受けた対象。
    /// </summary>
    public Virial.Game.IFakePlayer Target { get; set; }

    internal PlayerInteractionFailedForMyFakePlayerEvent(Virial.Game.Player user, Virial.Game.IFakePlayer target) : base(user)
    {
        User = user;
        Target = target;
    }
}

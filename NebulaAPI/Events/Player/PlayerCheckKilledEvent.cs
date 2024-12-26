using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;
using Virial.Text;

namespace Virial.Events.Player;

/// <summary>
/// このイベントはキラーのクライアント上でのみ発火します。
/// 計算のために必要な値はあらかじめ共有しておく必要があり、値の更新がある場合は更新も共有する必要があります。
/// </summary>
public class PlayerCheckKilledEvent : AbstractPlayerEvent
{
    public Virial.Game.Player Killer { get; private init; }
    public KillResult Result { get; set; } = KillResult.Kill;
    public CommunicableTextTag PlayerState { get; private init; }
    public CommunicableTextTag? EventDetail { get; private init; }
    public bool IsMeetingKill { get; private init; }

    internal PlayerCheckKilledEvent(Virial.Game.Player player, Virial.Game.Player killer, bool isMeetingKill, CommunicableTextTag playerState, CommunicableTextTag? eventDetail) : base(player)
    {
        Killer = killer;
        IsMeetingKill = isMeetingKill;
        PlayerState = playerState;
        EventDetail = eventDetail;
    }
}

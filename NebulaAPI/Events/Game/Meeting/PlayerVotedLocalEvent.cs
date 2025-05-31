using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 投票結果の開示時に呼び出されます。
/// このイベントによって、誰が自身に票を投じたか知ることができます。
/// </summary>
/// <remarks>
/// 自身に対する投票結果についてのみ呼び出されます。
/// </remarks>
public class PlayerVotedLocalEvent : AbstractPlayerEvent
{
    public IEnumerable<Virial.Game.Player> Voters { get { foreach (var v in voters) yield return v; } }
    private Virial.Game.Player[] voters;

    internal PlayerVotedLocalEvent(Virial.Game.Player player, Virial.Game.Player[] voters) : base(player)
    {
        this.voters = voters;
    }
}

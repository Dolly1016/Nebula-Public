using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Text;

namespace Virial.Events.Statistics;

/// <summary>
/// 統計的なイベントが発生すると呼び出されます。
/// </summary>
public class StatisticsEvent
{
    /// <summary>
    /// 統計的イベントに大きく関わるプレイヤー
    /// </summary>
    public Game.Player? Caused { get; internal init; }
    /// <summary>
    /// 統計的イベントの影響を受けるプレイヤー
    /// </summary>
    public ReadOnlyCollection<Game.Player> Related { get; internal init; }
    public CommunicableTextTag? Detail { get;internal init; }

    internal StatisticsEvent(Game.Player? caused, Game.Player[] related, CommunicableTextTag? eventDetail)
    {
        this.Caused = caused;
        this.Related = new(related);
        this.Detail = eventDetail;
    }
}

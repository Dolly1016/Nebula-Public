using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 緊急会議開始時に呼び出されます。
/// </summary>
public class MeetingStartEvent : Event
{
    /// <summary>
    /// 会議で投票を禁じる場合、<c>false</c>にしてください。
    /// このプロパティはプレイヤーの生死による投票の可否を表すものではありません。
    /// 本来投票しえない状況での投票を可能にしません。
    /// </summary>
    public bool CanVote { get; set; } = true;
    internal MeetingStartEvent() { }
}

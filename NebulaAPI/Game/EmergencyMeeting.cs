using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

public interface EmergencyMeeting : ILifespan
{
    /// <summary>
    /// 通報された死体です。
    /// </summary>
    Player? ReportedDeadBody { get; }

    /// <summary>
    /// 会議を始めたプレイヤーです。
    /// </summary>
    Player InvokedBy { get; }

    /// <summary>
    /// 会議の残り時間を変更します。
    /// </summary>
    /// <param name="deltaSec">残り時間に足しこむ秒数。正数を指定すると延長する。</param>
    void EditMeetingTime(int deltaSec);
}


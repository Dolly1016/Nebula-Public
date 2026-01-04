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
    /// 会議の残り時間を増減させます。
    /// 議論時間中に呼び出した場合、議論時間を増減させます。投票時間中に呼び出した場合、投票時間を増減させます。
    /// 残り議論時間より長い秒数を減少させる場合、投票時間も減少します。
    /// </summary>
    /// <param name="deltaSec">残り時間に足しこむ秒数。正数を指定すると延長する。</param>
    void EditMeetingTime(int deltaSec);
}


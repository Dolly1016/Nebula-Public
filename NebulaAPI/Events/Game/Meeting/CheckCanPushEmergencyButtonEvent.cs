using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 緊急会議ボタンが押せるか調べる際に呼び出されます。
/// 緊急会議をブロックできます。
/// </summary>
public class CheckCanPushEmergencyButtonEvent : Event
{
    /// <summary>
    /// 自身が緊急会議ボタンを押せるとき、trueを返します。
    /// </summary>
    public bool CanPushButton { get; private set; }
    internal string? CannotPushReason { get; private set; } = null;

    /// <summary>
    /// ボタンの使用をブロックします。
    /// </summary>
    /// <param name="reason">ボタンの禁止理由。翻訳済みのテキストを設定してください。</param>
    public void DenyButton(string? reason)
    {
        CanPushButton = false;
        CannotPushReason ??= reason;
    }

    internal CheckCanPushEmergencyButtonEvent()
    {
        CanPushButton = true;
    }
}

using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 会議終了時、追加追放者を含むプレイヤーが全員追放された後で呼び出されます。
/// このイベントで登録されたコルーチンを全クライアントが実行しきった後、<see cref="MeetingEndEvent"/>が呼び出されます。
/// </summary>
public class MeetingPreEndEvent : Event
{
    internal List<IEnumerator> Coroutines = new();

    /// <summary>
    /// 実行されるコルーチンを追加します。
    /// </summary>
    /// <param name="coroutine"></param>
    public void PushCoroutine(IEnumerator coroutine) => Coroutines.Add(coroutine);

    internal MeetingPreEndEvent()
    {
    }
}

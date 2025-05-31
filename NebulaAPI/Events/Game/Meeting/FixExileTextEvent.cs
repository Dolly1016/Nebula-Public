using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 追放テキストを決定するために呼び出されます。
/// </summary>
public class FixExileTextEvent : Event
{
    IReadOnlyList<Virial.Game.Player> exiled;
    internal FixExileTextEvent(IReadOnlyList<Virial.Game.Player> exiled) { this.exiled = exiled ?? []; }

    /// <summary>
    /// 追放されるプレイヤーを取得します。
    /// </summary>
    public IReadOnlyList<Virial.Game.Player> Exiled => exiled;

    private List<string> texts = [];

    /// <summary>
    /// 追放画面で表示されるテキストを追加します。
    /// </summary>
    /// <param name="text"></param>
    public void AddText(string text) => texts.Add(text);

    /// <summary>
    /// 追放画面で表示されるテキストを取得します。
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<string> GetTexts() => texts;
}

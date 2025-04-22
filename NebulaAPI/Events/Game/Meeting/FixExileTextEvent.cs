using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 追放テキストを決定するときに呼び出されます。
/// </summary>
internal class FixExileTextEvent : Event
{
    IReadOnlyList<Virial.Game.Player> exiled;
    internal FixExileTextEvent(IReadOnlyList<Virial.Game.Player> exiled) { this.exiled = exiled ?? []; }
    public IReadOnlyList<Virial.Game.Player> Exiled => exiled;

    private List<string> texts = [];
    public void AddText(string text) => texts.Add(text);
    public IReadOnlyList<string> GetTexts() => texts;
}

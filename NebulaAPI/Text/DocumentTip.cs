using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Virial.Text;

public interface IDocumentTip
{
    /// <summary>
    /// このDocumentTipが表示される場合はtrueを返します。
    /// </summary>
    public bool IsActive { get; }
    public string Text { get; }
}

public interface IDocumentTitledTip : IDocumentTip
{
    public string Title { get; }
}

public abstract class AbstractDocumentTip : IDocumentTip
{
    public bool IsActive => predicate.Invoke();
    private Func<bool> predicate;

    public string Text => text.Invoke();
    private Func<string> text;

    public AbstractDocumentTip(Func<bool> predicate, Func<string> text)
    {
        this.predicate = predicate;
        this.text = text;
    }
}

public abstract class AbstractDocumentTitledTip : AbstractDocumentTip, IDocumentTitledTip
{

    public string Title => title.Invoke();
    private Func<string> title;

    public AbstractDocumentTitledTip(Func<bool> predicate, Func<string> title, Func<string> text) : base(predicate, text)
    {
        this.title = title;
    }
}

public class WinConditionTip : AbstractDocumentTitledTip
{
    public GameEnd End { get; private init; }
    
    public WinConditionTip(GameEnd end, Func<bool> predicate, Func<string> title, Func<string> text) : base (predicate, title, text) 
    {
        End = end;
    }
}

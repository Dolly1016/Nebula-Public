using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Compat;

namespace Virial.Media;

/// <summary>
/// 文書検索用のテキストの断片です。
/// </summary>
/// <param name="Text"></param>
/// <param name="Widget"></param>
public record DocumentPiece(string[] Text, Func<GUIWidget> Widget, IDocument Document);

public interface IDocument
{
    /// <summary>
    /// クリック操作等によって画面遷移をする場合には、targetを使用することもできます。
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    GUIWidget? Build(Artifact<GUIScreen>? target);

    IEnumerable<DocumentPiece> Pieces { get; }
    string? CustomTitle => null;
    Image? Illustlation { get; }
    DefinedAssignable? RelatedAssignable => null;

    float? RequiredWidth => null;
    float? RequiredHeight => null;
    /// <summary>
    /// ドキュメントを非公開にする場合はfalseを返してください。
    /// </summary>
    bool CanBeShown => true;
}

public interface IDocumentWithId : IDocument
{
    void OnSetId(string documentId);
}
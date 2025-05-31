using System.Diagnostics.CodeAnalysis;
using Virial.Text;

namespace Virial.Assignable;

/// <summary>
/// 他Modからの引用情報を表します。
/// </summary>
public class Citation
{
    static private Dictionary<string, Citation> allCitations = new();

    /// <summary>
    /// 引用元Modのロゴ画像。
    /// </summary>
    public Media.Image? LogoImage { get; private init; }
    /// <summary>
    /// 引用元Modの表示名。翻訳テキストを使用する場合は言語パックに適切な翻訳が含まれている必要があります。
    /// </summary>
    public TextComponent Name { get; private init; }
    /// <summary>
    /// 引用元ModのURL。
    /// </summary>
    public string? RelatedUrl { get; private init; }

    /// <param name="id">引用のID。</param>
    /// <param name="logo">引用元Modのロゴ画像。</param>
    /// <param name="name">引用元Modの表示名。</param>
    /// <param name="relatedUrl">引用元ModのURL。適切なURLを指定してください。</param>
    public Citation(string id, Media.Image? logo, TextComponent name, string? relatedUrl)
    {
        LogoImage = logo;
        Name = name;
        RelatedUrl = relatedUrl;

        allCitations[id] = this;
    }

    /// <summary>
    /// IDから引用を取得します。
    /// </summary>
    /// <param name="id">引用のID。</param>
    /// <param name="citation">引用が存在する場合は引用を取得します。</param>
    /// <returns></returns>
    static public bool TryGetCitation(string id, [MaybeNullWhen(false)]out Citation citation)
    {
        return allCitations.TryGetValue(id, out citation);
    }
}

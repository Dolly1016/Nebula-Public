using System.Diagnostics.CodeAnalysis;
using Virial.Text;

namespace Virial.Assignable;

public class Citation
{
    static private Dictionary<string, Citation> allCitations = new();

    public Media.Image? LogoImage { get; private init; }
    public TextComponent Name { get; private init; }
    public string? RelatedUrl { get; private init; }
    
    public Citation(string id, Media.Image? logo, TextComponent name, string? relatedUrl)
    {
        LogoImage = logo;
        Name = name;
        RelatedUrl = relatedUrl;

        allCitations[id] = this;
    }

    static public bool TryGetCitation(string id, [MaybeNullWhen(false)]out Citation citation)
    {
        return allCitations.TryGetValue(id, out citation);
    }
}

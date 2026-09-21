using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Nebula.Http;

/// <summary>
/// ゲーム内のリッチテキスト（TextMeshPro のタグ）を HTML へ変換する。
/// </summary>
/// <remarks>
/// <para>
/// 変換対象の文字列にはプレイヤーが入力した名前が混ざる。閲覧画面は結果を innerHTML で流し込むので、
/// <b>タグとして解釈したもの以外は必ずエスケープする</b>。
/// </para>
/// <para>
/// タグの扱いは 3 通り。HTML に写せるものは写し、位置調整のように写しても意味を成さないものは落とし、
/// TextMeshPro のタグですらないものは文字として出す。最後のひとつが大事で、
/// <c>&lt;script&gt;</c> のような文字列を黙って消してしまわないようにしている
/// （ゲーム内でもタグとして解釈されず、そのまま表示される）。
/// </para>
/// <para>
/// 役職アイコン（<c>&lt;sprite name="…"&gt;</c>）は、ここでは位置を決めずに
/// <c>&lt;span class="role-icon" data-icon="…"&gt;</c> にするだけにしてある。
/// どのアトラスのどのマスかは閲覧画面が <c>/api/role-icons</c> の対応表を見て決める。
/// </para>
/// </remarks>
internal static class RichTextHtml
{
    /// <summary>入れ子を追えなくなるほど深いタグは、壊れた入力とみなして無視する。</summary>
    private const int MaxDepth = 32;

    /// <summary>
    /// TextMeshPro のタグではあるが、HTML に写しても意味を成さないので落とすもの。
    /// </summary>
    private static readonly HashSet<string> IgnoredTags = new(StringComparer.Ordinal)
    {
        "align", "allcaps", "alpha", "cspace", "font", "font-weight", "gradient", "indent",
        "line-height", "line-indent", "link", "lowercase", "margin", "mark", "mspace", "nobr",
        "noparse", "page", "pos", "rotate", "smallcaps", "space", "style", "sub", "sup",
        "uppercase", "voffset", "width",
    };

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var builder = new StringBuilder(text.Length + 32);
        //開いたまま残っている要素の閉じタグ。種類は対応する開始タグの名前で覚えておく。
        var open = new List<(string kind, string close)>();

        int index = 0;
        while (index < text.Length)
        {
            var c = text[index];

            if (c != '<')
            {
                AppendEscaped(builder, c);
                index++;
                continue;
            }

            var close = text.IndexOf('>', index + 1);
            if (close < 0)
            {
                //閉じられていない '<' は、ただの文字。
                AppendEscaped(builder, c);
                index++;
                continue;
            }

            var tag = text.Substring(index + 1, close - index - 1);
            var handled = tag.Length > 0 && (tag[0] == '/'
                ? CloseTag(builder, open, tag.Substring(1).Trim().ToLowerInvariant())
                : OpenTag(builder, open, tag.Trim()));

            //タグとして扱えなかったものは、そのまま文字として出す。
            if (!handled) AppendEscaped(builder, text, index, close + 1);

            index = close + 1;
        }

        //閉じ忘れは最後にまとめて閉じる
        for (int i = open.Count - 1; i >= 0; i--) builder.Append(open[i].close);

        return builder.ToString();
    }

    /// <returns>タグとして扱ったならtrue。文字として出すべきならfalse。</returns>
    private static bool OpenTag(StringBuilder builder, List<(string kind, string close)> open, string tag)
    {
        //<#RRGGBB> は <color=#RRGGBB> の略記
        if (tag[0] == '#')
        {
            if (ToCssColor(tag) == null) return false;
            PushColor(builder, open, tag);
            return true;
        }

        //タグ名は '=' か空白の手前まで。<sprite name="x"> のように属性が続くことがある。
        int nameEnd = 0;
        while (nameEnd < tag.Length && tag[nameEnd] != '=' && tag[nameEnd] != ' ') nameEnd++;

        var name = tag.Substring(0, nameEnd).ToLowerInvariant();
        var rest = tag.Substring(nameEnd).TrimStart();
        var value = rest.StartsWith("=", StringComparison.Ordinal) ? ReadValue(rest.Substring(1).TrimStart()) : "";

        switch (name)
        {
            case "color":
                PushColor(builder, open, value);
                return true;

            case "size":
                //ゲーム内で使うのは <size=NN%> だけ。他の単位はタグとしては正しいので、落とすだけにする。
                if (value.EndsWith("%", StringComparison.Ordinal)
                    && float.TryParse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
                    Push(builder, open, "size", $"<span style=\"font-size:{percentage.ToString("0.###", CultureInfo.InvariantCulture)}%\">", "</span>");
                return true;

            case "b":
            case "i":
            case "u":
            case "s":
                Push(builder, open, name, "<" + name + ">", "</" + name + ">");
                return true;

            case "br":
                builder.Append("<br>");
                return true;

            case "sprite":
                AppendSprite(builder, tag);
                return true;

            default:
                return IgnoredTags.Contains(name);
        }
    }

    /// <returns>タグとして扱ったならtrue。文字として出すべきならfalse。</returns>
    private static bool CloseTag(StringBuilder builder, List<(string kind, string close)> open, string name)
    {
        switch (name)
        {
            case "color":
            case "size":
            case "b":
            case "i":
            case "u":
            case "s":
                //素直な入れ子を前提にする。噛み合わない閉じタグは、余計なタグを吐かないよう捨てる。
                if (open.Count > 0 && open[^1].kind == name)
                {
                    builder.Append(open[^1].close);
                    open.RemoveAt(open.Count - 1);
                }
                return true;

            default:
                return IgnoredTags.Contains(name) || name == "sprite";
        }
    }

    private static void PushColor(StringBuilder builder, List<(string kind, string close)> open, string value)
    {
        var css = ToCssColor(value);
        //読めない色でも入れ子の数は合わせる。合わせないと後続の </color> がずれる。
        Push(builder, open, "color", css != null ? $"<span style=\"color:{css}\">" : "<span>", "</span>");
    }

    private static void Push(StringBuilder builder, List<(string kind, string close)> open, string kind, string start, string close)
    {
        if (open.Count >= MaxDepth) return;
        builder.Append(start);
        open.Add((kind, close));
    }

    /// <summary>
    /// アイコンは種類だけ書き出す。アトラス上の位置は閲覧画面が対応表を見て決める。
    /// </summary>
    private static void AppendSprite(StringBuilder builder, string tag)
    {
        //<sprite name="x"> のほかに <sprite index=0> もありうる。名前が引けないものは出さない。
        var nameIndex = tag.IndexOf("name=", StringComparison.OrdinalIgnoreCase);
        if (nameIndex < 0) return;

        var icon = ReadValue(tag.Substring(nameIndex + 5).TrimStart());
        if (icon.Length == 0) return;

        //属性値に入るので、引用符や山括弧が混ざっても壊れないようにする
        builder.Append("<span class=\"role-icon\" data-icon=\"");
        AppendEscaped(builder, icon, 0, icon.Length);
        builder.Append("\"></span>");
    }

    /// <summary>ゲーム内の色指定を CSS の色にする。読めなければ null。</summary>
    private static string? ToCssColor(string value)
    {
        if (value.Length == 0) return null;

        if (value[0] != '#')
        {
            //色名は CSS でもそのまま通る。余計なものを通さないよう英字だけに限る。
            foreach (var c in value) if (!IsAsciiLetter(c)) return null;
            return value.ToLowerInvariant();
        }

        var hex = value.Substring(1);
        foreach (var c in hex) if (!IsAsciiHexDigit(c)) return null;

        //TextMeshPro は末尾に不透明度を付けられる。CSS の #RRGGBBAA も同じ並び。
        return hex.Length is 3 or 4 or 6 or 8 ? "#" + hex : null;
    }

    /// <summary>タグの属性値を 1 つ読む。引用符があればその中身、無ければ空白の手前まで。</summary>
    private static string ReadValue(string value)
    {
        if (value.Length == 0) return "";

        var quote = value[0];
        if (quote == '"' || quote == '\'')
        {
            var end = value.IndexOf(quote, 1);
            return end < 0 ? value.Substring(1) : value.Substring(1, end - 1);
        }

        var space = value.IndexOf(' ');
        return space < 0 ? value : value.Substring(0, space);
    }

    //net6.0 には char.IsAsciiLetter / IsAsciiHexDigit が無い
    private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    private static bool IsAsciiHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static void AppendEscaped(StringBuilder builder, string text, int start, int end)
    {
        for (int i = start; i < end; i++) AppendEscaped(builder, text[i]);
    }

    private static void AppendEscaped(StringBuilder builder, char c)
    {
        switch (c)
        {
            case '&': builder.Append("&amp;"); break;
            case '<': builder.Append("&lt;"); break;
            case '>': builder.Append("&gt;"); break;
            case '"': builder.Append("&quot;"); break;
            case '\'': builder.Append("&#39;"); break;
            default: builder.Append(c); break;
        }
    }
}

using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.BugFix;

[HarmonyPatch(typeof(LetterTree), nameof(LetterTree.AddWord))]
internal static class LetterTreeAddWordPatch
{
    static private readonly Dictionary<string, string[]> ReplacedWords = new()
    {
        { "まん~", ["まんまん~", "まんげ", "まん毛"] },
        { "あそこ~", [] },
        { "アソコ~", [] },
        { "クリ~", ["クリ舐~", "クリイ~", "クリ吸~"] },
        { "かす~", [] },
        { "ごみ~", [] },
    };

    static internal readonly string[] AdditionalWords =
    [
        "にゃちん"
    ];

    //禁止ワードごとの許可語。キーは禁止ワードから末尾の'~','^','`'を除いたものになるので注意。
    static internal readonly Dictionary<string, string[]> AllowedWords = new()
    {
        { "チンコ", ["パチンコ"] },
        { "ちんこ", ["ぱちんこ"] },
        { "ﾁﾝｺ", ["ﾊﾟﾁﾝｺ"] },
        { "エロ", ["イエロー", "ピエロ"] },
        { "ゲス", ["ゲスト"] },
        { "カス", ["カスタ" /*カスタム、カスタマイズ、カスタマー、カスタネット*/, "カステラ"] },
        { "ケツ", ["バケツ", "フケツ"] },
        { "シネ", ["ハルシネーション", "シネマ"] },
        { "尻", ["尻尾"] },
        { "乳", ["牛乳", "豆乳", "乳製品", "乳酸"] },
        { "ばか", ["ばかり"] },
        { "めこ", ["ためこ", "はめこ"] },
        { "デブ", ["デブリ"] },
        { "バカ", ["バカンス"] },
    };

    static bool Prefix(LetterTree __instance, string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        if (ReplacedWords.TryGetValue(word, out var alternatives))
        {
            foreach (var alt in alternatives) AddWord(__instance, alt);
        }
        else
        {
            AddWord(__instance, word);
        }
        return false;
    }

    static private void AddWord(LetterTree tree, string word)
    {
        if (string.IsNullOrEmpty(word)) return;

        var node = tree.root;
        foreach (var l in word)
        {
            if ((!LetterTree.IsFiller(l, out var isUnicode) || isUnicode) && !IsTerminator(l))
                node = node.FindChild(l) ?? node.CreateChild(l);
        }

        if (node.Terminal != LetterTree.NodeTypes.NonTerm) return;

        node.Terminal = word[^1] switch
        {
            '~' => LetterTree.NodeTypes.TerminalStrict,
            '^' => LetterTree.NodeTypes.TerminalExact,
            '`' => LetterTree.NodeTypes.TerminalUnbroken,
            _ => LetterTree.NodeTypes.Terminal
        };
    }

    static private bool IsTerminator(char l) => l == '~' || l == '^' || l == '`';
}

internal static class LetterTreeSearchImpl
{
    static public bool IsCJKLetter(char c)
    {
        bool inRange =
            (c >= 'ᄀ' && c <= 'ᇿ') || //Hangul Jamo
            (c >= '々' && c <= '〇') || //々〆〇
            (c >= '぀' && c <= 'ヿ') || //Hiragana, Katakana
            (c >= '㄀' && c <= '㆏') || //Bopomofo, Hangul Compatibility Jamo
            (c >= 'ㆠ' && c <= 'ㆿ') || //Bopomofo Extended
            (c >= 'ㇰ' && c <= 'ㇿ') || //Katakana Phonetic Extensions
            (c >= '㐀' && c <= '䶿') || //CJK Unified Ideographs Extension A
            (c >= '一' && c <= '鿿') || //CJK Unified Ideographs
            (c >= 'ꥠ' && c <= '꥿') || //Hangul Jamo Extended-A
            (c >= '가' && c <= '퟿') || //Hangul Syllables, Hangul Jamo Extended-B
            (c >= '豈' && c <= '﫿') || //CJK Compatibility Ideographs
            (c >= 'ｦ' && c <= 'ￜ');   //Halfwidth Katakana, Halfwidth Hangul
        if (!inRange) return false;
        return char.IsLetter(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber;
    }

    //句読点・括弧類かどうか判定する。
    static public bool IsCJKPunctuation(char c)
    {
        bool inRange =
            (c >= '　' && c <= '〿') || //CJK Symbols and Punctuation
            c == '・' ||                    //Katakana Middle Dot
            (c >= '︰' && c <= '﹏') || //CJK Compatibility Forms
            (c >= '＀' && c <= '･');   //Fullwidth Forms, Halfwidth CJK Punctuation
        if (!inRange || c == '〜') return false;
        return char.IsPunctuation(c);
    }

    static private bool IsCJKBoundaryChar(char c) => IsCJKLetter(c) || IsCJKPunctuation(c);

    static private bool IsFiller(char c, out bool isUnicode)
    {
        var result = LetterTree.IsFiller(c, out isUnicode);
        return result && !(isUnicode && IsCJKBoundaryChar(c));
    }

    static public int Search(LetterTree tree, char[] input, int start)
    {
        if (start >= input.Length || IsFiller(input[start], out var isUnicode) && !isUnicode) return 0;
        bool exactStart = start == 0 || LetterTree.IsFiller(input[start - 1]);

        var path = new StringBuilder();
        string? matchedWord = null;
        int length = SubSearchRec(input, start, tree.root, false, false, exactStart, path, ref matchedWord);

        if (length > 0 && matchedWord != null && IsAllowed(input, start, length, matchedWord)) return 0;
        return length;
    }

    /// <summary>
    /// input[start..start+length)が、matchedWordに対応する許可語の出現範囲に収まっているかどうか。
    /// </summary>
    static private bool IsAllowed(char[] input, int start, int length, string matchedWord)
    {
        if (!LetterTreeAddWordPatch.AllowedWords.TryGetValue(matchedWord, out var allowed)) return false;

        //同じ文字の繰り返しは1文字として一致するため、先頭の繰り返しは除いて判定する(例: 「タスククリア」の「ククリ」)
        int end = start + length;
        while (start < end - 1 && input[start] == input[start + 1]) start++;
        length = end - start;

        foreach (var word in allowed)
        {
            //許可語の開始位置pは、p <= start かつ start + length <= p + word.Length を満たす
            for (int p = Math.Max(0, start + length - word.Length); p <= start; p++)
            {
                if (p + word.Length > input.Length) break;
                bool match = true;
                for (int i = 0; i < word.Length; i++)
                {
                    if (input[p + i] != word[i]) { match = false; break; }
                }
                if (match) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// pathには根から現在のノード(previous)までに辿った文字が入る。
    /// 一致した場合、一致した禁止ワードをmatchedWordに格納する。
    /// </summary>
    static private int SubSearchRec(char[] input, int start, LetterTree.LetterNode previous, bool postDupes, bool postBreak, bool exactStart, StringBuilder path, ref string? matchedWord)
    {
        if (start >= input.Length) return -2;

        char ch = input[start];
        if (IsFiller(ch, out var isUnicode) && (!isUnicode || previous.FindChild(ch) == null))
        {
            if (postDupes) return -2;
            int num = SubSearchRec(input, start + 1, previous, postDupes, true, exactStart, path, ref matchedWord);
            return num > 0 ? num + 1 : -2;
        }

        if (LetterTree.ToIndex(ch) == LetterTree.ToIndex(previous.Letter) && !postBreak)
        {
            int num = SubSearchRec(input, start + 1, previous, true, postBreak, exactStart, path, ref matchedWord);
            if (num > 0) return num + 1;
            if (previous.Terminal != LetterTree.NodeTypes.NonTerm)
            {
                matchedWord = path.ToString();
                return 1;
            }
        }

        var child = previous.FindChild(ch);
        //続きの無いCJKの文字・句読点は単語の切れ目とみなす
        if (child == null) return IsCJKBoundaryChar(ch) ? -2 : -3;

        path.Append(child.Letter);
        int num1 = SubSearchRec(input, start + 1, child, postDupes, postBreak, exactStart, path, ref matchedWord);
        if (num1 > 0)
        {
            path.Length--;
            return num1 + 1;
        }

        bool terminated = child.Terminal switch
        {
            LetterTree.NodeTypes.TerminalStrict => num1 == -2 && (exactStart || !postBreak),
            LetterTree.NodeTypes.TerminalUnbroken => num1 == -2 && !postBreak,
            LetterTree.NodeTypes.TerminalExact => num1 == -2 && exactStart,
            LetterTree.NodeTypes.Terminal => num1 <= 0,
            _ => false
        };
        if (terminated) matchedWord = path.ToString();
        path.Length--;
        return terminated ? 1 : num1;
    }

    static public string CensorWords(LetterTree tree, string chatText)
    {
        var charArray = chatText.ToCharArray();
        for (int start = 0; start < charArray.Length; ++start)
        {
            int num = Search(tree, charArray, start);
            if (num > 0)
            {
                for (int index = 0; index < num; ++index) charArray[start + index] = '*';
                start = start + num - 1;
            }
        }
        return new string(charArray);
    }
}

[HarmonyPatch(typeof(LetterTree), nameof(LetterTree.Search))]
internal static class LetterTreeSearchPatch
{
    static bool Prefix(LetterTree __instance, Il2CppStructArray<char> input, int start, ref int __result)
    {
        var array = new char[input.Length];
        for (int i = 0; i < array.Length; i++) array[i] = input[i];
        __result = LetterTreeSearchImpl.Search(__instance, array, start);
        return false;
    }
}

/// <summary>
/// 言語ごとの禁止ワードを読み込んだ後に、追加の禁止ワードを登録する。
/// </summary>
[HarmonyPatch(typeof(BlockedWords), nameof(BlockedWords.SetLanguage))]
internal static class BlockedWordsSetLanguagePatch
{
    static void Postfix()
    {
        var tree = BlockedWords.SkipList;
        if (tree == null) return;
        foreach (var word in LetterTreeAddWordPatch.AdditionalWords) tree.AddWord(word);
    }
}

[HarmonyPatch(typeof(BlockedWords), nameof(BlockedWords.ContainsWord))]
internal static class BlockedWordsContainsWordPatch
{
    static bool Prefix(string chatText, ref bool __result)
    {
        var tree = BlockedWords.SkipList;
        if (tree == null) return true;

        var charArray = chatText.ToCharArray();
        __result = false;
        for (int start = 0; start < charArray.Length; ++start)
        {
            if (LetterTreeSearchImpl.Search(tree, charArray, start) > 0)
            {
                __result = true;
                break;
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(BlockedWords), nameof(BlockedWords.CensorWords))]
internal static class BlockedWordsCensorWordsPatch
{
    static bool Prefix(string chatText, ref string __result)
    {
        var tree = BlockedWords.SkipList;
        if (tree == null) return true;

        __result = LetterTreeSearchImpl.CensorWords(tree, chatText);
        return false;
    }
}

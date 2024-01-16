using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Text;

/// <summary>
/// 翻訳キーに一意的な意味を与えるタグです。
/// </summary>
public interface CommunicableTextTag
{
    /// <summary>
    /// 翻訳キーを取得します。
    /// </summary>
    public string TranslationKey { get; }

    internal int Id { get; }
    internal string Text { get; }
}

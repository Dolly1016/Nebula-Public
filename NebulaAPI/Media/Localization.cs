using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Media;

public interface Translator
{
    string Translate(string key);
}

public static class Localization
{
    static public Translator? CurrentLanguage => NebulaAPI.instance.Language;

    /// <summary>
    /// 現在の言語で翻訳されたテキストを返します。
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    static public string Translate(string key) => CurrentLanguage?.Translate(key) ?? "*" + key;
}

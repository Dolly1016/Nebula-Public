using Nebula.Patches;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Virial.Runtime;
using static Il2CppSystem.Net.Http.Headers.Parser;

namespace Nebula.Modules;

public class EastAsianFontChanger
{
    private static TMPro.TMP_FontAsset? FontJP = null, FontSC = null, FontKR = null;

    public static void LoadFont()
    {
        var fonts = UnityEngine.Object.FindObjectsOfTypeIncludingAssets(Il2CppType.Of<TMPro.TMP_FontAsset>());
        foreach (var font in fonts)
        {
            if (font.name == "NotoSansJP-Regular SDF")
                FontJP = font.CastFast<TMPro.TMP_FontAsset>();
            if (font.name == "NotoSansSC-Regular SDF")
                FontSC = font.CastFast<TMPro.TMP_FontAsset>();
            if (font.name == "NotoSansKR-Regular SDF")
                FontKR = font.CastFast<TMPro.TMP_FontAsset>();
        }
    }
    public static void SetUpFont(string language)
    {
        TMPro.TMP_FontAsset? localFont = null;
        
        if (language == "Korean")
            localFont = FontKR;
        else if (language == "SChinese" || language == "TChinese")
            localFont = FontSC;
        else
            localFont = FontJP;

        if (localFont == null) return;

        var fonts = UnityEngine.Object.FindObjectsOfTypeIncludingAssets(Il2CppType.Of<TMPro.TMP_FontAsset>());
        foreach (var font in fonts)
        {
            var asset = font.CastFast<TMPro.TMP_FontAsset>();
            asset.fallbackFontAssetTable.Clear();

            if (font.name == localFont.name) continue;

            asset.fallbackFontAssetTable.Add(localFont);
            if (localFont != FontJP) asset.fallbackFontAssetTable.Add(FontJP);
            if (localFont != FontSC) asset.fallbackFontAssetTable.Add(FontSC);
            if (localFont != FontKR) asset.fallbackFontAssetTable.Add(FontKR);
        }
    }
}

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public static class AddonDefaultLanguageLoader
{
    static AddonDefaultLanguageLoader()
    {
        foreach (var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenStream("Language/English.dat");
            if (stream != null) Language.DefaultLanguage!.Deserialize(stream);
        }
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class Language
{
    private class LanguageAPI : Virial.Media.Translator
    {
        string Virial.Media.Translator.Translate(string key) => Language.Translate(key);
    }

    static public readonly Virial.Media.Translator API = new LanguageAPI();
    static private Language? CurrentLanguage = null;
    static private Language? GuestLanguage = null;
    static internal Language? DefaultLanguage = null;
    static public Versioning LanguageVersion = new(); 
    /// <summary>
    /// 関数は言語を変更してもリセットしない。
    /// </summary>
    static private Dictionary<string, Func<string>> functionalMap = [];

    public Dictionary<string, string> translationMap = [];

    public static void Register(string key, Func<string> function) => functionalMap[key] = function;

    public static void SetGuestLanguage(Language? language) => GuestLanguage = language;

    public static string GetCurrentLanguage() => GetLanguage((uint)AmongUs.Data.DataManager.Settings.Language.CurrentLanguage);
    
    
    public static IEnumerable<uint> AllLanguageId()
    {
        for (uint i = 0; i <= 15; i++) yield return i;
    }

    public static IEnumerable<string> AllLanguageName()
    {
        for (uint i = 0; i <= 15; i++) yield return GetLanguage(i);
    }

    public static string GetLanguageShownString(uint language)
    {
        return TranslationController.Instance.Languages[(SupportedLangs)language].Name;
    }
    public static string GetLanguage(uint language)
    {
        return language switch
        {
            0 => "English",
            1 => "Latam",
            2 => "Brazilian",
            3 => "Portuguese",
            4 => "Korean",
            5 => "Russian",
            6 => "Dutch",
            7 => "Filipino",
            8 => "French",
            9 => "German",
            10 => "Italian",
            11 => "Japanese",
            12 => "Spanish",
            13 => "SChinese",
            14 => "TChinese",
            15 => "Irish",
            _ => "English",
        };
    }

    public static Stream? OpenDefaultLangStream() => StreamHelper.OpenFromResource("Nebula.Resources.Lang.dat");
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Language Data");

        DefaultLanguage = new Language();
        using (var stream = StreamHelper.OpenFromResource("Nebula.Resources.Color.dat")) DefaultLanguage.Deserialize(stream);
        using (var stream = OpenDefaultLangStream()) DefaultLanguage.Deserialize(stream);
        using (var stream = StreamHelper.OpenFromResource("Nebula.Resources.SecretLang.dat")) DefaultLanguage.Deserialize(stream);
        DefaultLanguage.translationMap["empty"] = "";

        EastAsianFontChanger.LoadFont();
    }

    public static void ReflectFallBackFont()
    {
        string lang = GetLanguage((uint)AmongUs.Data.DataManager.Settings.Language.CurrentLanguage);
        EastAsianFontChanger.SetUpFont(lang);
    }

    public static void OnChangeLanguage(uint language)
    {
        LanguageVersion.Update();

        string lang = GetLanguage(language);
        EastAsianFontChanger.SetUpFont(lang);

        CurrentLanguage = new Language();

        //CurrentLanguage.Deserialize(StreamHelper.OpenFromResource("Nebula.Resources.Languages." + lang + ".dat"));
        //CurrentLanguage.Deserialize(StreamHelper.OpenFromResource("Nebula.Resources.Languages." + lang + "_Help.dat"));

        foreach(var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenStream("Language/" + lang + ".dat");
            if (stream != null) CurrentLanguage.Deserialize(stream, name => addon.OpenStream("Language/" + lang + "_" + name + ".dat"));

            foreach(var s in addon.FindStreams("Language/" + lang + "/", path => path.EndsWith(".dat")))
            {
                CurrentLanguage.Deserialize(s);
            }
        }
    }

    public void Deserialize(Stream? stream, Func<string, Stream?>? subStreamProvider = null) => Deserialize(stream, (key, text) => translationMap[key] = text, subStreamProvider: subStreamProvider);
    static public void Deserialize(Stream? stream, Action<string,string> pairAction, Action<string>? commentAction = null, Func<string, Stream?>? subStreamProvider = null)
    {
        if (stream == null) return;
        using var reader = new StreamReader(stream, Encoding.GetEncoding("utf-8"));
        string? line;
        string[] strings;
        int pairs = 0;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length < 3)
            {
                commentAction?.Invoke("");
                continue;
            }

            if (line[0] == '#')
            {
                commentAction?.Invoke(line);
                continue;
            }

            if (line[0] == '$')
            {
                if (subStreamProvider != null)
                {
                    using var subStream = subStreamProvider.Invoke(line.Substring(1));
                    if (subStream != null) Deserialize(subStream, pairAction, commentAction, subStreamProvider);
                }
                continue;
            }

            strings = line.Split(':', 2);

            if (strings.Length != 2)
            {
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Language, "Failed to read the line \"" + line + "\"");
                continue;
            }

            for (int i = 0; i < 2; i++)
            {
                int first = strings[i].IndexOf('"') + 1;
                int last = strings[i].LastIndexOf('"');

                try
                {
                    strings[i] = strings[i].Substring(first, last - first);
                }
                catch
                {
                    NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Language, "Cannot read the line \"" + line + "\"");
                    continue;
                }
            }



            pairAction.Invoke(strings[0], strings[1]);
            pairs++;
        }
    }

    public static string TranslateIfNotNull(string? translationKey, string ifNull = "") => translationKey == null ? ifNull : Translate(translationKey);
    public static string Translate(string? translationKey)
    {
        if (translationKey == null) return "Invalid Key";

        if (functionalMap.TryGetValue(translationKey, out var func))
        {
            return func.Invoke();
        }
        else
        {
            return Find(translationKey) ?? "*" + translationKey;
        }
    }

    public static bool TryTranslate(string? translationKey,[MaybeNullWhen(false)] out string translated)
    {
        translated = Find(translationKey);
        return translated != null;
    }

    public static string? Find(string? translationKey)
    {
        if (translationKey == null) return null;
        string? result;
        if (GuestLanguage?.TryGetText(translationKey, out result) ?? false)
            return result!;
        if (CurrentLanguage?.TryGetText(translationKey, out result) ?? false)
            return result!;
        if (DefaultLanguage?.TryGetText(translationKey, out result) ?? false)
            return result!;
        return null;
    }

    public static string? FindFromDefault(string? translationKey)
    {
        return (DefaultLanguage?.TryGetText(translationKey, out var result) ?? false) ? result : null;
    }

    public bool HasKey(string? translationKey)
    {
        if (translationKey == null) return false;
        return translationMap.ContainsKey(translationKey);
    }

    public bool TryGetText(string? translationKey, [MaybeNullWhen(false)] out string text)
    {
        if (translationKey == null)
        {
            text = null;
            return false;
        }
        return translationMap.TryGetValue(translationKey,out text);
    }
}

[HarmonyPatch(typeof(LanguageSetter), nameof(LanguageSetter.SetLanguage))]
public static class ChangeLanguagePatch
{
    public static void Postfix(LanguageSetter __instance, [HarmonyArgument(0)] LanguageButton selected)
    {
        Language.OnChangeLanguage((uint)AmongUs.Data.DataManager.Settings.Language.CurrentLanguage);
    }
}

//Do not rewrite the text provided by this class.
//If you would like to translate, please send your translation proposal to Dolly, who will then approve it.
public static class BuiltInLanguage
{
    public record TextSet(
        string MarketplaceTermsTitle,
        string MarketplaceTermsContent,
        string MarketplaceTermsOk,
        string MarketplaceTermsReject
        );
    
    private static Dictionary<uint, TextSet> texts = [];

    static BuiltInLanguage()
    {
        texts[0 /*English*/] = new(
            "Marketplace Terms of Service",
            "The Marketplace allows users to download user-created costumes and addons.<br>We request that developers of costumes and addons ensure their creations do not infringe upon the rights of others and adhere to standards of public decency. regarding fan works or derivative content based on third-party IPs, please strictly observe the guidelines provided by the respective rights holders.<br><br>Please be aware that all liability regarding costumes and addons lies solely with the creator of the work. Neither Innersloth nor NoS assumes any responsibility. We reserve the right to remove or suspend the availability of any content if a dispute or violation occurs.<br><br>For issues regarding costumes or addons, please contact the developer of the content first. If the issue remains unresolved, you may contact NoS. Innersloth is not involved in any issues related to these costumes and addons. Please strictly refrain from contacting Innersloth regarding these matters.<br>Nebula on the Ship Contact: nebula.on.the.ship@gmail.com",
            "Accept",
            "Decline"
            );
        texts[11 /*Japanese*/] = new(
            "マーケットプレイスのご利用にあたって",
            "マーケットプレイスでは、ユーザが制作したコスチュームやアドオンをダウンロードできます。<br><br>コスチュームおよびアドオンの開発者には、他者の権利を侵害せず、公序良俗に沿った制作をしていただくようお願い申し上げます。他IPの二次創作に関しては、権利者の提示するガイドラインを遵守してください。<br><br>コスチュームおよびアドオンに関する責任はすべてその制作物の作者に帰属します。InnerslothおよびNoSは責任を負いかねますので、ご了承ください。トラブルが発生した場合、当該コスチュームおよびアドオンの公開を停止する措置をとる可能性がございます。<br><br>トラブルに関してご相談なさる場合は、コスチュームおよびアドオンの開発者、次いでNoSにお問い合わせください。コスチュームおよびアドオンに関して発生したトラブルについて、Innerslothは一切関係ありませんから、Innerslothへのお問い合わせは厳にお控えください。<br>Nebula on the Ship お問い合わせ先: nebula.on.the.ship@gmail.com",
            "同意する",
            "同意しない"
            );
        texts[13 /*SChinese*/] = new(
            "创意工坊使用条款",
            "创意工坊允许玩家下载由其他玩家创作的装饰品和插件。<br><br>我们要求装饰品和插件的开发者确保其作品不侵犯他人权利，并遵守公共礼仪标准。关于基于第三方知识产权的衍生内容，请严格遵守各权利方提供的指导原则。<br><br>请注意，与装饰品和插件相关的所有责任均由作品的创作者独自承担。Innersloth和NoS均不承担任何责任。若发生争议或违规行为，我们保留移除或暂停任何内容的权利。<br><br>如遇与装饰品或插件相关的问题，请首先联系该内容的开发者。若问题未得到解决，您可以联系NoS。Innersloth不参与任何与这些装饰品和插件相关的问题，请避免就因这些事而联系Innersloth开发人员。<br>NoS联系方式nebula.on.the.ship@gmail.com",
            "接受",
            "拒绝");
        texts[14 /*TChinese*/] = new(
            "創意工坊使用條款",
            "創意工坊允許玩家下載由其他玩家創作的裝飾品和插件。<br><br>我們要求裝飾品和插件的開發者確保其作品不侵犯他人權利，並遵守公共禮儀標準。關於基於第三方知識產權的衍生內容，請嚴格遵守各權利方提供的指導原則。<br><br>請注意，與裝飾品和插件相關的所有責任均由作品的創作者單獨承擔。Innersloth和NoS均不承擔任何責任。若發生爭議或違規行為，我們保留移除或暫停任何內容的權利。<br><br>如遇與裝飾品或插件相關的問題，請首先聯繫該內容的開發者。若問題未得到解決，您可以聯繫NoS。Innersloth不參與任何與這些裝飾品和插件相關的問題，請避免就因這些事而聯繫Innersloth開發人員。<br>NoS聯繫方式：nebula.on.the.ship@gmail.com",
            "接受",
            "拒絕");
    }

    public static BuiltInLanguage.TextSet CurrentTextSet => texts.TryGetValue((uint)AmongUs.Data.DataManager.Settings.Language.CurrentLanguage, out var textSet) ? textSet : texts[0];
}
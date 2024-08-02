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

    static public Virial.Media.Translator API = new LanguageAPI();
    static private Language? CurrentLanguage = null;
    static private Language? GuestLanguage = null;
    static internal Language? DefaultLanguage = null;

    public Dictionary<string, string> translationMap = new();

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
        switch (language)
        {
            case 0:
                return "English";
            case 1:
                return "Latam";
            case 2:
                return "Brazilian";
            case 3:
                return "Portuguese";
            case 4:
                return "Korean";
            case 5:
                return "Russian";
            case 6:
                return "Dutch";
            case 7:
                return "Filipino";
            case 8:
                return "French";
            case 9:
                return "German";
            case 10:
                return "Italian";
            case 11:
                return "Japanese";
            case 12:
                return "Spanish";
            case 13:
                return "SChinese";
            case 14:
                return "TChinese";
            case 15:
                return "Irish";
        }
        return "English";
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
        string lang = GetLanguage(language);
        EastAsianFontChanger.SetUpFont(lang);

        CurrentLanguage = new Language();

        //CurrentLanguage.Deserialize(StreamHelper.OpenFromResource("Nebula.Resources.Languages." + lang + ".dat"));
        //CurrentLanguage.Deserialize(StreamHelper.OpenFromResource("Nebula.Resources.Languages." + lang + "_Help.dat"));

        foreach(var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenStream("Language/" + lang + ".dat");
            if (stream != null) CurrentLanguage.Deserialize(stream);
        }
    }

    public void Deserialize(Stream? stream) => Deserialize(stream, (key, text) => translationMap[key] = text);
    static public void Deserialize(Stream? stream, Action<string,string> pairAction, Action<string>? commentAction = null)
    {
        if (stream == null) return;
        using (var reader = new StreamReader(stream, Encoding.GetEncoding("utf-8"))) {
            string? line;
            string[] strings;
            int pairs = 0;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length < 3)
                {
                    if (commentAction != null) commentAction("");
                    continue;
                }

                if (line[0] == '#')
                {
                    if(commentAction != null) commentAction(line);
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
                        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Language,"Cannot read the line \"" + line + "\"");
                        continue;
                    }
                }

                

                pairAction.Invoke(strings[0], strings[1]);
                pairs++;
            }
        }
    }

    public static string Translate(string? translationKey)
    {
        if (translationKey == null) return "Invalid Key";
        return Find(translationKey) ?? "*" + translationKey;
    }

    public static string? Find(string? translationKey)
    {
        if (translationKey == null) return null;
        string? result;
        if (GuestLanguage?.translationMap.TryGetValue(translationKey, out result) ?? false)
            return result!;
        if (CurrentLanguage?.translationMap.TryGetValue(translationKey, out result) ?? false)
            return result!;
        if (DefaultLanguage?.translationMap.TryGetValue(translationKey, out result) ?? false)
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

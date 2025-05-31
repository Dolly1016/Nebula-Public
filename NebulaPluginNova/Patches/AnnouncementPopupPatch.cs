using AmongUs.Data.Player;
using AmongUs.Data;
using Assets.InnerNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Net;
using Virial.Runtime;

namespace Nebula.Patches;

public class ModNews
{
    [JsonSerializableField]
    public int id;

    [JsonSerializableField]
    public string title = "";

    [JsonSerializableField]
    public string shortTitle = "";

    [JsonSerializableField]
    public string subTitle = "";

    [JsonSerializableField]
    public string date = "";

    [JsonSerializableField]
    public string detail = "";

    [JsonSerializableField]
    public bool debug = false;

    public Announcement ToAnnouncement()
    {
        var result = new Announcement();
        result.Number = id;
        result.Title = title;
        result.SubTitle = subTitle;
        result.ShortTitle = shortTitle;
        result.Text = detail;
        result.Language = (uint)DataManager.Settings.Language.CurrentLanguage;
        result.Date = date ?? "";
        result.Id = "ModNews";

        return result;
    }
}


[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public static class ModNewsLoader
{
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading News");
        yield return ModNewsHistory.GetLoaderEnumerator();
    }
}

[HarmonyPatch]
public class ModNewsHistory
{
    public static List<ModNews> AllModNews = new List<ModNews>();

    private static Regex RoleRegex = new Regex("%ROLE:[A-Z]+\\([^)]+\\)%");
    private static Regex OptionRegex = new Regex("%LANG\\([a-zA-Z\\.0-9]+\\)\\,\\([^)]+\\)%");

    public static IEnumerator GetLoaderEnumerator()
    {
        if (!NebulaPlugin.AllowHttpCommunication) yield break;

        AllModNews.Clear();

        var lang = Language.GetCurrentLanguage();

        HttpClient http = new HttpClient();
        http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        System.Uri uri;
        try
        {
            uri = new(Helpers.ConvertUrl($"https://raw.githubusercontent.com/Dolly1016/Nebula/master/Announcement_{lang}.json"));
        }
        catch
        {
            yield break;
        }

        var task = http.GetAsync(uri, HttpCompletionOption.ResponseContentRead);
        while (!task.IsCompleted) yield return null;
        var response = task.Result;


        if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
        {
            yield break;
        }

        AllModNews = JsonStructure.Deserialize<List<ModNews>>(response.Content.ReadAsStringAsync().Result) ?? new();

        foreach (var news in AllModNews)
        {
            foreach (Match match in RoleRegex.Matches(news.detail))
            {
                var split = match.Value.Split(':', '(', ')');
                FormatRoleString(match, ref news.detail, split[1], split[2]);
            }

            foreach (Match match in OptionRegex.Matches(news.detail))
            {
                var split = match.Value.Split('(', ')');

                var translated = Language.Find(split[1]);
                if (translated == null) translated = split[3];
                news.detail = news.detail.Replace(match.Value, translated);
            }
        }
    }

    private static void FormatRoleString(Match match, ref string str, string key, string defaultString)
    {
        foreach (var role in Roles.Roles.AllAssignables())
        {
            if (role.LocalizedName.ToUpper() == key)
            {
                str = str.Replace(match.Value, role.DisplayColoredName);
            }
        }
        str = str.Replace(match.Value, defaultString);
    }


    

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    public static bool SetModAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {        
        List<Announcement> temp = new(aRange);
        temp.AddRange(AllModNews.Where(m => !m.debug).Select(m => m.ToAnnouncement()));
        temp.Sort((a1, a2) => { return string.Compare(a2.Date, a1.Date); });
        aRange = temp.ToArray();
        
        return true;
    }

    static SpriteLoader ModLabel = SpriteLoader.FromResource("Nebula.Resources.NebulaNewsIcon.png", 100f);

    [HarmonyPatch(typeof(AnnouncementPanel), nameof(AnnouncementPanel.SetUp)), HarmonyPostfix]
    public static void SetUpPanel(AnnouncementPanel __instance, [HarmonyArgument(0)] Announcement announcement)
    {
        if (announcement.Number < 100000) return;
        var obj = new GameObject("ModLabel");
        obj.layer = LayerExpansion.GetUILayer();
        obj.transform.SetParent(__instance.transform);
        obj.transform.localPosition = new Vector3(-0.8f, 0.13f, 0.5f);
        obj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = ModLabel.GetSprite();
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }
}

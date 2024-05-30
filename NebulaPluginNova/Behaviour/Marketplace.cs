using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;
using Virial.Media;
using Virial.Text;

namespace Nebula.Behaviour;

public class MarketplaceExhibit
{
    [JsonSerializableField]
    public int EntryId;

    [JsonSerializableField]
    public string Title;

    [JsonSerializableField]
    public string Blurb;

    [JsonSerializableField]
    public string Detail;

    [JsonSerializableField]
    public string DeveloperId;

    [JsonSerializableField]
    public string Developer;

    [JsonSerializableField]
    public bool IsAddon;

    [JsonSerializableField]
    public string Url;
}

public class OwningMarketplaceItem
{
    [JsonSerializableField]
    public int EntryId;

    [JsonSerializableField]
    public string OwningVersion;
}

public record MarketplaceItem(int EntityId, string Title, string Short, string Url)
{
    public string? Detail { get; set; } = null;
}

public class Marketplace : MonoBehaviour
{
    static Marketplace() => ClassInjector.RegisterTypeInIl2Cpp<Marketplace>();

    private MetaScreen myScreen = null!;

    private List<Func<(IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm)>> screenLayer = new();
    private Func<bool>? currentConfirm = null;
    private const float ScreenWidth = 9f;

    protected void Close()
    {
        TransitionFade.Instance.DoTransitionFade(gameObject, null!, () => MainMenuManagerInstance.MainMenu?.mainMenuUI.SetActive(true), () => GameObject.Destroy(gameObject));
    }

    static public void Open(MainMenuManager mainMenu)
    {
        MainMenuManagerInstance.MainMenu = mainMenu;

        var obj = UnityHelper.CreateObject<Marketplace>("MarketplaceMenu", Camera.main.transform, new Vector3(0, 0, -30f));
        TransitionFade.Instance.DoTransitionFade(null!, obj.gameObject, () => { mainMenu.mainMenuUI.SetActive(false); }, obj.OnShown);
    }

    static private TextComponent TextCosmetics = new TranslateTextComponent("marketplace.type.cosmetics");
    static private TextComponent TextAddons = new TranslateTextComponent("marketplace.type.addons");
    static private TextComponent TextSearch = new RawTextComponent(">");
    public void OnShown()
    {
        bool isAddon = true;
        TextMeshPro buttonText = null!;

        bool searching = false;

        bool lastIsAddon = false;
        string lastQuery = "";
        int lastPage = 0;
        List<GUIWidget> lastContents = new List<GUIWidget>();

        var textField = new GUITextField(Virial.Media.GUIAlignment.Center, new(4.8f, 0.4f)) { IsSharpField = false, HintText = Language.Translate("marketplace.ui.hint").Color(Color.gray) };
        var viewer = new GUIScrollView(Virial.Media.GUIAlignment.Center, new(7f, 3.9f), null);

        IEnumerator CoSetToViewer(OnlineMarketplace.SearchContentResult[]? result)
        {
            if (result != null)
            {
                lastContents.AddRange(result.Select(r => new NoSGUIFramed(Virial.Media.GUIAlignment.Left, new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left,
                    new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left,
                        GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.MarketplaceTitle), Uri.UnescapeDataString(r.title)),
                        GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.MarketplaceDeveloper), Uri.UnescapeDataString(r.author))
                    ),
                    GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.MarketplaceBlurb), Uri.UnescapeDataString(r.blurb))
                ), new(0.1f, 0.1f), Color.clear)
                { 
                    PostBuilder = renderer =>
                    {
                        renderer.transform.localPosition = new(0, 0, 0.1f);
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        var button = renderer.gameObject.SetUpButton(true, renderer, Color.clear, UnityEngine.Color.Lerp(UnityEngine.Color.cyan, UnityEngine.Color.green, 0.4f).AlphaMultiplied(0.3f));
                        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                        collider.size = renderer.size;
                        //button.OnClick.AddListener();
                    }
                }));
                viewer.InnerArtifact.Do(screen => screen.SetWidget(new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center, lastContents), out _));
                searching = false;
            }
            yield break;
        }

        var widget = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.TopLeft,
            new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OblongHeader), new TranslateTextComponent("marketplace.ui.title")),
            new GUIFixedView(Virial.Media.GUIAlignment.TopLeft, new(9.5f, 0.65f),
            new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.TopLeft,
                new GUIButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsButtonMedium), TextAddons)
                {
                    PostBuilder = (t) => buttonText = t,
                    OnClick = _ =>
                    {
                        isAddon = !isAddon;
                        buttonText.text = (isAddon ? TextAddons : TextCosmetics).GetString();
                    }
                },
                textField,
                new GUIButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsButton), TextSearch)
                {
                    OnClick = clickable =>
                    {
                        if (searching) return;

                        var text = textField.Artifact.FirstOrDefault()?.Text ?? "";
                        if (text.Length == 0)
                        {
                            textField.Artifact.Do(field => field.SetHint("Please enter a keyword".Color(Color.Lerp(Color.red, Color.gray, 0.35f))));
                        }
                        else
                        {
                            lastContents.Clear();

                            lastIsAddon = isAddon;
                            lastQuery = text!.Trim();
                            lastPage = 0;
                            searching = true;
                            StartCoroutine(OnlineMarketplace.CoSearchContents(isAddon, lastQuery, 0, result => CoSetToViewer(result)).WrapToIl2Cpp());
                        }
                    }
                },
                new NoSGUIMargin(GUIAlignment.Center, new(0.25f, 0f)),
                new GUIButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OptionsButtonMedium), new TranslateTextComponent("marketplace.ui.publish"))
                {
                    OnClick = _ =>
                    {
                        
                    }
                }
            )),
            viewer
            );

        myScreen.SetBorder(new(9f, 5f));
        myScreen.SetWidget(widget, new(0f, 1f), out _);
    }

    public void Awake()
    {
        myScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close());
    }

}

internal static class OnlineMarketplace
{
    private const string APIURL = "https://script.google.com/macros/s/AKfycbxlRptoER8nf8CUAR-LX-shMCqFIghhZdpyVYBjcvH38E-z9wyNawBMpyJLis-4-nU1/exec";

    private class OnlineResponse<T>
    {
        [JsonSerializableField]
        public int statusCode;

        [JsonSerializableField]
        public T data;
    }

    static private IEnumerator CoGetResponse<T>(string method, Func<T, IEnumerator> callback, params (string label, string value)[] contents)
    {
        var json = contents.Prepend(("request", method)).Select(tuple => (tuple.Item1, Uri.EscapeDataString(tuple.Item2))).Join(tuple => $"\"{tuple.Item1}\" : \"{tuple.Item2}\"", ",");
        var content = new StringContent("{" + json + "}", Encoding.UTF8, @"application/json");
        var task = NebulaPlugin.HttpClient.PostAsync(APIURL, content);
        yield return task.WaitAsCoroutine();
        var strTask = task.Result.Content.ReadAsStringAsync();
        yield return strTask.WaitAsCoroutine();
        yield return callback.Invoke(JsonStructure.Deserialize<OnlineResponse<T>>(strTask.Result)!.data);
    }

    public class SearchContentResult
    {
        [JsonSerializableField]
        public string entryId;

        [JsonSerializableField]
        public string title;

        [JsonSerializableField]
        public string blurb;

        [JsonSerializableField]
        public string author;

        [JsonSerializableField]
        public string url;
    }
    private class SearchResult
    {
        [JsonSerializableField]
        public bool success;

        [JsonSerializableField]
        public List<SearchContentResult> result;

    }
    private class PublishResult
    {
        [JsonSerializableField]
        public int id;

    }

    static public IEnumerator CoSearchContents(bool isAddon, string query, int page, Func<SearchContentResult[]?,IEnumerator> callback)
    {
        yield return CoGetResponse<SearchResult>("search", s => callback.Invoke(s.success ? s.result.ToArray() : null), ("type", isAddon ? "addon" : "cosmetics"), ("query", query), ("page", page.ToString()));
    }

    static public IEnumerator CoPushContent(bool isAddon, string title, string blurb, string detail, string author, string url, string key, string discordId, Func<int?, IEnumerator> callback)
    {
        yield return CoGetResponse<PublishResult>("publish", r => callback.Invoke(r.id), ("type", isAddon ? "addon" : "cosmetics"), ("title", title), ("short", blurb), ("detail", detail), ("author", author), ("url", url), ("key", key), ("discordId", discordId));
    }
}
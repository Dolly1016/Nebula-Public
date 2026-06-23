using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using System.Text.Json;
using System.Text.Json.Serialization;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Behavior;

public class DeveloperMarketplaceItem
{
    static private readonly char[] alphabets = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    static public char[] Alphabets => alphabets;
    static public string GetRandomizedString(int length) => new(Enumerable.Repeat(Alphabets.Random, length).Select(f => f.Invoke()).ToArray());

    public int EntryId = -1;

    public string Title;
    public string Blurb;
    public string Detail;
    public string Author;
    public bool IsAddon;
    public string Url;
    public string Discord;
    public string Key = GetRandomizedString(64);

    public IEnumerator TryOnlineCheck(Action onSucceeded, Action onFailed)
    {
        LocalMarketplaceItem item = new() { Url = Url };
        yield return NebulaWebRequest.CoGet(IsAddon ? item.ToAddonUrl : (item.ToCostumeUrl + "/Contents.json"), true, _ => onSucceeded.Invoke(), onFailed);
    }
}

public class LocalMarketplaceItem
{
    [JsonSerializableField]
    public int EntryId;
    [JsonSerializableField]
    public string Title;
    [JsonSerializableField]
    public string Url;
    [JsonSerializableField]
    public bool AutoUpdate = false;

    public string ToCostumeUrl => Helpers.ConvertUrl("https://raw.githubusercontent.com/" + Url);
    public string ToAddonUrl => Helpers.ConvertUrl("https://api.github.com/repos/" + Url + "/releases/latest");
}

public class DevMarketplaceItem : LocalMarketplaceItem
{
    [JsonSerializableField]
    public string Key;
}

public record MarketplaceItem(int EntityId, string Title, string Short, string Url)
{
    public string? Detail { get; set; } = null;
}

public class MyMarketplaceStructure
{
    [JsonSerializableField]
    public List<LocalMarketplaceItem> OwningCostumes = [];
    [JsonSerializableField]
    public List<LocalMarketplaceItem> OwningAddons = [];

    [JsonSerializableField]
    public List<DevMarketplaceItem> DevCostumes = [];
    [JsonSerializableField]
    public List<DevMarketplaceItem> DevAddons = [];

    [JsonSerializableField]
    public bool Confirmed = false;
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class MarketplaceData
{
    private static readonly JsonDataSaver<MyMarketplaceStructure> DataSaver = new("Marketplace");
    static internal MyMarketplaceStructure Data => DataSaver.Data!;
    static internal void Save() => DataSaver.Save();
    static public bool CheckOwning(int entryId)
    {
        return Data.OwningCostumes.Any(c => c.EntryId == entryId) || Data.OwningAddons.Any(c => c.EntryId == entryId);
    }
}

public class Marketplace : MonoBehaviour
{
    static Marketplace() => ClassInjector.RegisterTypeInIl2Cpp<Marketplace>();
    static bool IsInLobby = false;

    private MetaScreen MarketplaceScreen = null!;
    private MetaScreen MyItemsScreen = null!;

    private Func<bool>? currentConfirm = null;
    private const float ScreenWidth = 9f;

    public void Close()
    {
        TransitionFade.Instance.DoTransitionFade(gameObject, null!, () => MainMenuManagerInstance.MainMenu?.mainMenuUI.SetActive(true), () => GameObject.Destroy(gameObject));
    }

    static public void Open(MainMenuManager mainMenu)
    {
        IsInLobby = false;
        MainMenuManagerInstance.SetPrefab(mainMenu);

        var obj = UnityHelper.CreateObject<Marketplace>("MarketplaceMenu", Camera.main.transform, new Vector3(0, 0, -30f));
        ModSingleton<Marketplace>.Instance = obj;
        TransitionFade.Instance.DoTransitionFade(null!, obj.gameObject, () => { mainMenu.mainMenuUI.SetActive(false); }, () => { obj.SetUp(); obj.ShowMarketplaceScreen(); });
    }

    static public void OpenInLobby()
    {
        IsInLobby = true;
        var obj = UnityHelper.CreateObject<Marketplace>("MarketplaceMenu", Camera.main.transform, new Vector3(0, 0, -30f));

        ModSingleton<Marketplace>.Instance = obj;
        TransitionFade.Instance.DoTransitionFade(null!, obj.gameObject, () => { }, () => { obj.SetUp(); obj.ShowMarketplaceScreen(); });
    }

    void SetUp()
    {
        SetUpMarketplaceScreen();
        SetUpItemsScreen();

        if (!MarketplaceData.Data.Confirmed)
        {
            var window = MetaScreen.GenerateWindow(new(7.3f, 4.3f), HudManager.InstanceExists ? HudManager.Instance.transform : null, VVector3.Zero, true, false, true, BackgroundSetting.Modern, false);
            var textSet = BuiltInLanguage.CurrentTextSet;
            TextAttribute contentAttr = new(GUI.API.GetAttribute(AttributeAsset.DocumentStandard)) { FontSize = new(1.5f, 1.4f, 1.5f, true), Wrapping = true, Size = new(7.05f, 3.6f) };
            window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
                GUI.API.RawText(GUIAlignment.Center, AttributeAsset.DocumentTitle, textSet.MarketplaceTermsTitle),
                GUI.API.VerticalMargin(0.1f),
                GUI.API.RawText(GUIAlignment.Center, contentAttr, textSet.MarketplaceTermsContent),
                GUI.API.VerticalMargin(0.1f),
                GUI.API.HorizontalHolder(GUIAlignment.Center,
                    GUI.API.RawButton(GUIAlignment.Center, AttributeAsset.OptionsValue, textSet.MarketplaceTermsReject, _ => {
                        window.CloseScreen();
                        Close();
                    }),
                    GUI.API.RawButton(GUIAlignment.Center, AttributeAsset.OptionsValue, textSet.MarketplaceTermsOk, _ => {
                        MarketplaceData.Data.Confirmed = true;
                        MarketplaceData.Save();
                        window.CloseScreen();
                    })
                )
                ), new Vector2(0.5f, 0.6f), out _);
        }
    }

    void ShowMarketplaceScreen()
    {
        MarketplaceScreen.gameObject.SetActive(true);
        MyItemsScreen.gameObject.SetActive(false);
    }

    void ShowMyItemScreen()
    {
        MarketplaceScreen.gameObject.SetActive(false);
        MyItemsScreen.gameObject.SetActive(true);
    }

    static private readonly TextComponent TextCosmetics = new TranslateTextComponent("marketplace.type.cosmetics");
    static private readonly TextComponent TextAddons = new TranslateTextComponent("marketplace.type.addons");
    static private readonly TextComponent TextSearch = new RawTextComponent(">");

    static public MetaScreen OpenDetailWindow(bool isAddon, int entryId, Transform? parent = null, Action? onUpdate = null)
    {
        var window = MetaScreen.GenerateWindow(new(7f, 3.6f), parent, VVector3.Zero, true, true);
        window.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, new GUILoadingIcon(GUIAlignment.Center) { Size = 0.3f }, new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayTitle), new TranslateTextComponent("marketplace.ui.loading"))), new Vector2(0.5f,0.5f), out _);

        void ShowDetail(OnlineMarketplace.ItemDetail? result)
        {
            if (result == null) return;
            if (!window) return;

            List<LocalMarketplaceItem> owningItems = (isAddon ? MarketplaceData.Data.OwningAddons : MarketplaceData.Data.OwningCostumes);
            var localItem = owningItems.FirstOrDefault(item => item.EntryId == entryId);
            bool owning = localItem != null;
            bool canObtain = result.Url != null && result.Title != null;
            Virial.Color color = owning ? Virial.Color.Red : canObtain ? new(1f,1f,0f) : Virial.Color.Gray;

            string GetFromResult(string? str, string translateKey)
            {
                if (str != null) return Uri.UnescapeDataString(str);
                else return Language.Translate(translateKey);
            }
            var blurb = GetFromResult(result.Blurb, "marketplace.ui.marketplace.unknownBlurb");
            var title = GetFromResult(result.Title, "marketplace.ui.marketplace.unknownTitle");
            var detail = GetFromResult(result.Title, "marketplace.ui.marketplace.unknownDetail");
            var author = GetFromResult(result.Author, "marketplace.ui.marketplace.unknownAuthor");

            window.SetWidget(new GUIScrollView(GUIAlignment.Center, new(6.8f, 3.5f), new VerticalWidgetsHolder(GUIAlignment.Center,
                new HorizontalWidgetsHolder(GUIAlignment.Left, GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.MarketplaceTitle), title), GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), author)),
                GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), blurb),
                GUI.API.VerticalMargin(0.1f),
                new HorizontalWidgetsHolder(GUIAlignment.Left,
                    GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), "marketplace.ui.marketplace.state"),
                    GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), ":"),
                    new NoSGUIMargin(GUIAlignment.Center, new(0.12f, 0f)),
                    GUI.API.Text(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.LeftBoldFixed), GUI.API.TextComponent(owning ? Virial.Color.Green : Virial.Color.Red, owning ? "marketplace.ui.marketplace.state.owning" : "marketplace.ui.marketplace.state.unowning")),
                    GUI.API.Button(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.MarketplacePublishButton), GUI.API.TextComponent(color, owning ? "marketplace.ui.marketplace.deactivate" : canObtain ? "marketplace.ui.marketplace.activate" : "marketplace.ui.marketplace.invalid"), clickable =>
                    {
                        if (owning)
                        {
                            owningItems.RemoveAll(item => item.EntryId == entryId);
                            MetaUI.ShowConfirmDialog(parent, new TranslateTextComponent("marketplace.ui.marketplace.inactivated"));
                        }
                        else if(canObtain)
                        {
                            LocalMarketplaceItem item = new (){ EntryId = entryId, Title = title, Url = Uri.UnescapeDataString(result.Url!) };
                            owningItems.Add(item);
                            if (!isAddon)
                            {
                                var _ = MoreCosmic.LoadOnlineExtra(item, item.ToCostumeUrl);
                            }

                            MetaUI.ShowConfirmDialog(parent, new TranslateTextComponent("marketplace.ui.marketplace.activated" + (isAddon ? ".addons" : ".cosmetics")));
                        }
                    
                        MarketplaceData.Save();
                        ShowDetail(result);

                        onUpdate?.Invoke();
                    }, color: color),
                    new NoSGUIMargin(GUIAlignment.Center, new(0.25f,0f)),
                    (isAddon && owning) ? new HorizontalWidgetsHolder(GUIAlignment.Center, new NoSGUICheckbox(GUIAlignment.Left, localItem!.AutoUpdate) { OnValueChanged = val => { localItem.AutoUpdate = val; MarketplaceData.Save(); } }, GUI.API.HorizontalMargin(0.1f),  GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.DocumentBold), "marketplace.ui.marketplace.autoUpdate")) : GUIEmptyWidget.Default
                ),
                GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), detail.Replace("\r","<br>"))
                )), out _);
        }

        NebulaManager.Instance.StartCoroutine(OnlineMarketplace.CoGetContent(entryId, result => ManagedEffects.Action(()=>ShowDetail(result))).WrapToIl2Cpp());

        return window;
    }

    void SetUpMarketplaceScreen()
    {
        bool isAddon = false;

        bool searching = false;
        
        //一度でも検索したらtrue
        bool searchedAlready = false;

        bool lastIsAddon = false;
        string lastQuery = "";
        int lastPage = 0;
        List<GUIWidget> lastContents = [];

        var textField = new GUITextField(Virial.Media.GUIAlignment.Center, new(4.8f, 0.4f)) { IsSharpField = false, HintText = Language.Translate("marketplace.ui.hint").Color(VColor.Gray) };
        var viewer = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 3.9f), null);

        IEnumerator CoSetToViewer(OnlineMarketplace.SearchResult? result)
        {
            if (result?.Items != null)
            {
                lastContents.AddRange(result.Items.Select(r => new NoSGUIFramed(Virial.Media.GUIAlignment.Left, new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left,
                    GUI.API.Text(GUIAlignment.Left, AttributeAsset.DocumentStandard, GUI.API.FunctionalTextComponent(() => MarketplaceData.CheckOwning(r.Id) ? Language.Translate("marketplace.ui.marketplace.state.owning").Color(VColor.Green) : Language.Translate("marketplace.ui.marketplace.state.unowning").Color(VColor.Red))),
                    GUI.API.VerticalMargin(-0.05f),
                    new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.Left,
                        GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.MarketplaceTitle, Uri.UnescapeDataString(r.Title)),
                        GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.MarketplaceDeveloper, Uri.UnescapeDataString(r.Author))
                    ),
                    GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.MarketplaceBlurb, Uri.UnescapeDataString(r.Blurb))
                ), new(0.1f, 0.1f), VColor.Clear)
                { 
                    OnClicked = () =>
                    {
                        OpenDetailWindow(isAddon, r.Id, transform, () =>
                        {
                            if (MyItemsScreen.isActiveAndEnabled) UpdateItemsScreen();
                            if (MarketplaceScreen.isActiveAndEnabled) RefreshMarketplaceInnerScreen();
                        });
                    }
                }));
                viewer.InnerArtifact.Do(screen => screen.SetWidget(new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center, lastContents), out _));
                searching = false;
            }
            yield break;
        }

        void UpdateCategory(bool addon)
        {
            if (isAddon != addon)
            {
                isAddon = addon;
                RefreshMarketplaceInnerScreen();
            }
        }

        void RefreshMarketplaceInnerScreen()
        {
            lastContents.Clear();
            StartCoroutine(OnlineMarketplace.CoGetLatestContents(isAddon, 0, CoSetToViewer).WrapToIl2Cpp());
        }

        Virial.Media.GUIWidget GenerateCategoryWidget(bool forAddon) => new GUIModernButton(Virial.Media.GUIAlignment.Center, Virial.Text.AttributeAsset.MarketplaceCategoryButton, forAddon ? TextAddons : TextCosmetics)
        {
            OnClick = _ => UpdateCategory(forAddon),
            SelectedDefault = !forAddon,
            WithCheckMark = true
        };

        var widget = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.TopLeft,
            new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OblongHeader, new TranslateTextComponent("marketplace.ui.title")),
            GUI.API.HorizontalHolder(GUIAlignment.Center,
                GUI.API.VerticalHolder(GUIAlignment.Top, GenerateCategoryWidget(false), GenerateCategoryWidget(true)).AsButtonGroup().Enmask(),
                GUI.API.VerticalHolder(GUIAlignment.Left,
#if PC
                    GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.TopLeft,
                        textField,
                        new GUIButton(Virial.Media.GUIAlignment.Center, Virial.Text.AttributeAsset.OptionsButton, TextSearch)
                        {
                            OnClick = clickable =>
                            {
                                if (searching) return;

                                viewer.InnerArtifact.Do(a => a.SetStaticWidget(new GUILoadingIcon(GUIAlignment.Center) { Size = 0.35f }, new(0.5f, 0.5f), out _));
                                var text = textField.Artifact.FirstOrDefault()?.Text ?? "";


                                lastContents.Clear();

                                lastIsAddon = isAddon;
                                lastQuery = text!.Trim();
                                lastPage = 0;
                                searching = true;

                                searchedAlready = true;
                                if (text.Length == 0)
                                {
                                    StartCoroutine(OnlineMarketplace.CoGetLatestContents(isAddon, 0, CoSetToViewer).WrapToIl2Cpp());
                                }
                                else
                                {
                                    StartCoroutine(OnlineMarketplace.CoSearchContents(isAddon, lastQuery, 0, CoSetToViewer).WrapToIl2Cpp());
                                }

                            }
                        }
                    ).Enmask(),
#endif
                    viewer
                )));

        MarketplaceScreen.SetBorder(new(9f, 5f));
        MarketplaceScreen.SetWidget(widget, new VVector2(0f, 1f), out _);
        StartCoroutine(OnlineMarketplace.CoGetLatestContents(isAddon, 0, CoSetToViewer).WrapToIl2Cpp());
    }

    (Func<bool, IEnumerable<LocalMarketplaceItem>> items, Action<LocalMarketplaceItem> action) currentItemsAction;

    void SetActionOnItemsScreen((Func<bool, IEnumerable<LocalMarketplaceItem>> items, Action<LocalMarketplaceItem> action) action)
    {
        currentItemsAction = action;
        if (MyItemsScreen.isActiveAndEnabled) UpdateItemsScreen();
    }

    void UpdateItemsScreen()
    {
        SetOnItemsScreen(currentItemsAction.items.Invoke(isAddonOnItemsScreen));
    }

    private bool isAddonOnItemsScreen = false;
    private GUIScrollView viewerOnItemsScreen;
    private void SetOnItemsScreen(IEnumerable<LocalMarketplaceItem>? result)
    {
        List<GUIWidget> contents = new(result?.Select(r => new NoSGUIFramed(Virial.Media.GUIAlignment.Left,
                GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.MarketplaceTitle), Uri.UnescapeDataString(r.Title))
       , new(0.1f, 0.1f), VColor.Clear)
        {
            PostBuilder = renderer =>
            {
                renderer.transform.localPosition = new(0, 0, 0.1f);
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var button = renderer.gameObject.SetUpButton(true, renderer, VColor.Clear, Virial.Color.Lerp(VColor.Cyan, VColor.Green, 0.4f).AlphaMultiplied(0.3f));
                var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                collider.size = renderer.size;
                button.OnClick.AddListener(() => currentItemsAction.action.Invoke(r));
            }
        }) ?? []);
        viewerOnItemsScreen.InnerArtifact.Do(screen => screen.SetWidget(new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Center, contents), out _));
    }

    void SetUpItemsScreen()
    {   
        viewerOnItemsScreen = new GUIScrollView(Virial.Media.GUIAlignment.Center, new(4.2f, 3.9f), null);

        Virial.Media.GUIWidget GenerateCategoryButton(bool forAddon) => new GUIModernButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.MarketplaceCategoryButton), forAddon ? TextAddons : TextCosmetics)
        {
            OnClick = _ =>
            {
                if (isAddonOnItemsScreen != forAddon)
                {
                    isAddonOnItemsScreen = forAddon;
                    SetOnItemsScreen(currentItemsAction.items.Invoke(isAddonOnItemsScreen));
                }
            }, 
            SelectedDefault = !forAddon, WithCheckMark = true
        };

        var widget = new VerticalWidgetsHolder(Virial.Media.GUIAlignment.Top,
            new NoSGUIText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OblongHeader), new TranslateTextComponent("marketplace.ui.title")),
            GUI.API.VerticalHolder(GUIAlignment.Left,
            new HorizontalWidgetsHolder(Virial.Media.GUIAlignment.TopLeft, GenerateCategoryButton(false), GenerateCategoryButton(true)).Enmask().AsButtonGroup(),
            viewerOnItemsScreen
            ).Move(new(2.4f, 0f)));

        MyItemsScreen.SetBorder(new(9f, 5f));
        MyItemsScreen.SetWidget(widget, new VVector2(0f, 1f), out _);
    }

    public void Awake()
    {
        (string key, Action action, bool defaultSelection, bool cannotSelect)[] buttons = [
            ("marketplace", () => ShowMarketplaceScreen(), true, false), 
            ("inventory", () => { ShowMyItemScreen(); SetActionOnItemsScreen((isAddon => (isAddon ? MarketplaceData.Data?.OwningAddons : MarketplaceData.Data?.OwningCostumes) ?? [], item =>OpenDetailWindow(isAddonOnItemsScreen, item.EntryId, transform, () => { if (MyItemsScreen.isActiveAndEnabled) UpdateItemsScreen(); }))); }, false, false),
#if PC
            ("contents", () => { ShowMyItemScreen(); SetActionOnItemsScreen((isAddon => (isAddon ? MarketplaceData.Data?.DevAddons : MarketplaceData.Data?.DevCostumes) ?? [], item => EditContent(isAddonOnItemsScreen, (item as DevMarketplaceItem)!))); }, false, false),
            ("publish", () => { ShowPublishWindow(); }, false, true),
#endif
            ];
        
        //ロビー内では公開・管理機能はナシ。
        if(IsInLobby) buttons = buttons.Take(2).ToArray();

        var tabScreen = MetaScreen.GenerateScreen(new(8f, 1f), transform, new(2.2f, 1.9f, -10f), false, false, false);
        tabScreen.SetWidget(new GUIFixedView(GUIAlignment.Center, new(8f,1f), new HorizontalWidgetsHolder(GUIAlignment.Center, buttons.Select(b => new GUIModernButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.MarketplaceTabButton), new TranslateTextComponent("marketplace.tab." + b.key)) { OnClick = _ => b.action.Invoke(), SelectedDefault = b.defaultSelection, BlockSelectingOnClicked = b.cannotSelect }))).AsButtonGroup(), new Vector2(0f,0.5f),out _);
        
        MarketplaceScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close());
        MyItemsScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close(), true);
    }

    void EditContent(bool isAddon, DevMarketplaceItem item)
    {
        var waitWindow = MetaScreen.GenerateWindow(new(3f, 1f), transform, VVector3.Zero, true, true, withMask: true);
        waitWindow.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, new GUILoadingIcon(GUIAlignment.Center) { Size = 0.35f }, GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), "marketplace.ui.edit.fetch")), new Vector2(0.5f, 0.5f), out _);

        StartCoroutine(OnlineMarketplace.CoGetContent(item.EntryId, result =>
        {
            waitWindow.CloseScreen();
            return ManagedEffects.Action(() =>
            {
                if (result == null)
                    MetaUI.ShowConfirmDialog(transform, new TranslateTextComponent("marketplace.ui.edit.fetch.error"));
                else
                    ShowEditWindow(isAddon, edited => {
                        var waitUpdateWindow = MetaScreen.GenerateWindow(new(3f, 1f), transform, VVector3.Zero, true, true, withMask: true);
                        waitUpdateWindow.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, new GUILoadingIcon(GUIAlignment.Center) { Size = 0.35f }, GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), "marketplace.ui.edit.wait")), new Vector2(0.5f, 0.5f), out _);

                        StartCoroutine(OnlineMarketplace.CoEditContent(edited.EntryId, edited.Key, edited.Title, edited.Blurb, edited.Detail, edited.Author, edited.Url, result => {
                            waitUpdateWindow.CloseScreen();
                            return ManagedEffects.Action(() => MetaUI.ShowConfirmDialog(transform, new TranslateTextComponent((result?.ResultId == 0) ? "marketplace.ui.edit.finish" : "marketplace.ui.edit.failed")));
                        }).WrapToIl2Cpp());
                    }, new()
                    {
                        Title = Uri.UnescapeDataString(result.Title),
                        Author = Uri.UnescapeDataString(result.Author),
                        Blurb = Uri.UnescapeDataString(result.Blurb),
                        Detail = Uri.UnescapeDataString(result.Detail),
                        Url = Uri.UnescapeDataString(result.Url),
                        Key = item.Key,
                        EntryId = item.EntryId,
                        IsAddon = isAddon,
                    });
            });
        }).WrapToIl2Cpp());
    }

    void ShowEditWindow(bool isAddon, Action<DeveloperMarketplaceItem> callback, DeveloperMarketplaceItem? item = null)
    {
        var window = MetaScreen.GenerateWindow(new(8f, 4.8f), transform, VVector3.Zero, true, true, withMask: true);

        (GUIWidget widget, GUITextField textField) TextField(bool isMultiple, float width, string translateKey, string? defaultText = null)
        {
            var textField = new GUITextField(GUIAlignment.Left, new(width, isMultiple ? 2.2f : 0.22f)) { HintText = Language.Translate("marketplace.ui.publish." + translateKey + ".hint").Color(VColor.Gray), TextPredicate = c => c != '"', IsSharpField = false, MaxLines = isMultiple ? 10 : 1, FontSize = isMultiple ? 1.5f : 1.8f, DefaultText = defaultText ?? "" };
            return (new HorizontalWidgetsHolder(GUIAlignment.Left, GUI.API.LocalizedText(isMultiple ? GUIAlignment.TopLeft : GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OptionsTitleShortest), "marketplace.ui.publish." + translateKey), textField), textField);
        }

        bool withDiscord = item == null;

        (var nameWidget, var nameField) = TextField(false, 5f, "name", item?.Title);
        (var authorWidget, var authorField) = TextField(false, 2f, "author", item?.Author);
        (var discordWidget, var discordField) = TextField(false, 2f, "discord");
        (var repositoryWidget, var repositoryField) = TextField(false, 5f, "repository" + (isAddon ? ".addons" : ".cosmetics"), item?.Url);
        (var blurbWidget, var blurbField) = TextField(false, 5f, "blurb", item?.Blurb);
        (var detailWidget, var detailField) = TextField(true, 6.5f, "detail", item?.Detail);
        GUITextField[] fields = withDiscord ? [nameField, authorField, discordField, repositoryField, blurbField, detailField] : [nameField, authorField, repositoryField, blurbField, detailField];

        bool isChecking = false;

        IEnumerator CoCheckAndInvokeCallback()
        {
            

            bool flag = false;
            foreach (var field in fields)
            {
                field.Artifact.Do(a =>
                {
                    if (a.Text.Length == 0) {
                        flag = true;
                        a.SetHint(Language.Translate("marketplace.ui.publish.blankField").Color(new VColor(0.6f, 0f, 0f)));
                    }
                });
            }
            if (flag) yield break;
            

            isChecking = true;

            yield return item.TryOnlineCheck(() =>
            {
                callback.Invoke(item);
                window.CloseScreen();
            }, () =>
            {
                repositoryField.Artifact.Do(t => { t.SetText(""); t.SetHint(Language.Translate("marketplace.ui.publish.invalidRepos" + (isAddon ? ".addons" : ".cosmetics")).Color(new VColor(0.6f, 0f, 0f))); });
            });

            isChecking = false;
        }

        window.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, nameWidget, withDiscord ?new HorizontalWidgetsHolder(GUIAlignment.Left, authorWidget, discordWidget) : authorWidget, repositoryWidget, blurbWidget, detailWidget, new NoSGUIMargin(GUIAlignment.Center, new(0f, 0.15f)), GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.MarketplacePublishButton), "marketplace.ui.publish.publish", _ =>
        {
            if (!isChecking)
            {
                item ??= new();
                item.Title = nameField.Artifact.First().Text;
                item.Author = authorField.Artifact.First().Text;
                if (withDiscord) item.Discord = discordField.Artifact.First().Text;
                item.Url = repositoryField.Artifact.First().Text;
                item.Blurb = blurbField.Artifact.First().Text;
                item.Detail = detailField.Artifact.First().Text;
                item.IsAddon = isAddon;

                StartCoroutine(CoCheckAndInvokeCallback().WrapToIl2Cpp());
            }
        })), out var windowSize);
    }

    void ShowPublishWindow()
    {

        var typeWindow = MetaScreen.GenerateWindow(new(3f, 1f), transform, VVector3.Zero, true, true, withMask: true);
        GUIWidget TypeButton(bool isAddon) => GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButtonMedium), "marketplace.type." + (isAddon ? "addons" : "cosmetics"),
            clickable =>
            {
                typeWindow.CloseScreen();
                ShowEditWindow(isAddon, item =>
                {
                    var waitWindow = MetaScreen.GenerateWindow(new(3f, 1f), transform, VVector3.Zero, true, true, withMask: true);
                    waitWindow.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, new GUILoadingIcon(GUIAlignment.Center) { Size = 0.35f }, GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.DocumentStandard), "marketplace.ui.publish.wait")), new VVector2(0.5f, 0.5f), out _);

                    IEnumerator CoFinishPublish(bool success)
                    {
                        waitWindow.CloseScreen();

                        MetaUI.ShowConfirmDialog(transform, new TranslateTextComponent(success ? "marketplace.ui.publish.finish" : "marketplace.ui.publish.failed"));
                        yield break;
                    }
                    StartCoroutine(OnlineMarketplace.CoPushContent(item.IsAddon, item.Title, item.Blurb, item.Detail, item.Author, item.Url, item.Key, item.Discord, result =>
                    {
                        bool succeed = result != null && result.ResultId == 0;
                        if (succeed)
                        {
                            if (isAddon)
                                MarketplaceData.Data.DevAddons.Add(new() { EntryId = result!.ItemId, Title = item.Title, Key = item.Key, Url = item.Url });
                            else
                                MarketplaceData.Data.DevCostumes.Add(new() { EntryId = result!.ItemId, Title = item.Title, Key = item.Key, Url = item.Url });
                            MarketplaceData.Save();
                        }
                        return CoFinishPublish(succeed);
                    }).WrapToIl2Cpp());
                });
            });

        typeWindow.SetWidget(new VerticalWidgetsHolder(GUIAlignment.Center, GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayContent),"marketplace.ui.publish.selectTypes"), new NoSGUIMargin(GUIAlignment.Center, new(0f,0.2f)), new HorizontalWidgetsHolder(GUIAlignment.Center, TypeButton(true), TypeButton(false))), new Vector2(0.5f, 0.5f), out _);
    }

}

internal static class OnlineMarketplace
{
    static private IEnumerator CoPostResponse<TRequest, TResponse>(string method, TRequest requestData, Func<TResponse, IEnumerator> callback) where TRequest : class where TResponse : class
    {
        if (NebulaPlugin.AllowHttpCommunication)
        {
            IEnumerator? callbackCoroutine = null;
            yield return NebulaWebRequest.CoPost<TRequest, TResponse>(NebulaWebRequest.GetNoSAPI($"marketplace/{method}/"), requestData, result => {
                callbackCoroutine = callback.Invoke(result);
            });
            if (callbackCoroutine != null) yield return callbackCoroutine;
        }
        yield break;
    }

    public class PublishRequest
    {
        [JsonPropertyName("isAddon")]
        public bool IsAddon { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("blurb")]
        public string Blurb { get; set; }
        [JsonPropertyName("detail")]
        public string Detail { get; set; }
        [JsonPropertyName("author")]
        public string Author { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("key")]
        public string Key { get; set; }
        [JsonPropertyName("discordId")]
        public string DiscordId { get; set; }
        [JsonPropertyName("langs")]
        public string[] Langs { get; set; }
        [JsonPropertyName("icon")]
        public string? Icon { get; set; } = null;
    }

    public class EditRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("key")]
        public string Key { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("blurb")]
        public string Blurb { get; set; }
        [JsonPropertyName("detail")]
        public string Detail { get; set; }
        [JsonPropertyName("author")]
        public string Author { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("langs")]
        public string[] Langs { get; set; }
        [JsonPropertyName("icon")]
        public string? Icon { get; set; } = null;
    }

    public class VisibilityRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("key")]
        public string Key { get; set; }
        [JsonPropertyName("isPublic")]
        public bool IsPublic { get; set; }
    }

    public class SearchRequest
    {
        [JsonPropertyName("isAddon")]
        public bool IsAddon { get; set; }
        [JsonPropertyName("query")]
        public string Query { get; set; }
        [JsonPropertyName("page")]
        public int Page { get; set; }
        [JsonPropertyName("lang")]
        public string Lang { get; set; }
    }
    public class LatestRequest
    {
        [JsonPropertyName("isAddon")]
        public bool IsAddon { get; set; }
        [JsonPropertyName("page")]
        public int Page { get; set; }
        [JsonPropertyName("lang")]
        public string Lang { get; set; }
    }

    public class DetailRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class PublishResult
    {
        [JsonPropertyName("resultId")]
        public int ResultId { get; set; }
        [JsonPropertyName("itemId")]
        public int ItemId { get; set; }
    }

    public class EditResult
    {
        [JsonPropertyName("resultId")]
        public int ResultId { get; set; }
    }

    public class VisibilityResult
    {
        [JsonPropertyName("resultId")]
        public int ResultId { get; set; }
    }

    public class ItemAbst
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("blurb")]
        public string Blurb { get; set; }
        [JsonPropertyName("author")]
        public string Author { get; set; }
        [JsonPropertyName("langSupport")]
        public bool LangSupport { get; set; }
        [JsonPropertyName("icon")]
        public string Icon { get; set; }
    }

    public class SearchResult
    {
        [JsonPropertyName("resultId")]
        public int ResultId { get; set; }
        [JsonPropertyName("items")]
        public ItemAbst[] Items { get; set; }
    }

    public class ItemDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("isAddon")]
        public bool IsAddon { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("blurb")]
        public string Blurb { get; set; }
        [JsonPropertyName("detail")]
        public string Detail { get; set; }
        [JsonPropertyName("author")]
        public string Author { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("langs")]
        public string[] Langs { get; set; }
    }

    static public IEnumerator CoGetLatestContents(bool isAddon, int page, Func<SearchResult?, IEnumerator> callback)
    {
        yield return CoPostResponse<LatestRequest, SearchResult>("latest", new() { IsAddon = isAddon, Page = page, Lang = Language.GetCurrentLanguage() }, callback);
    }

    static public IEnumerator CoSearchContents(bool isAddon, string query, int page, Func<SearchResult?, IEnumerator> callback)
    {
        yield return CoPostResponse<SearchRequest, SearchResult>("search", new() { IsAddon = isAddon, Query = query, Page = page, Lang = Language.GetCurrentLanguage()}, callback);
    }

    static public IEnumerator CoPushContent(bool isAddon, string title, string blurb, string detail, string author, string url, string key, string discordId, Func<PublishResult?, IEnumerator> callback)
    {
        yield return CoPostResponse<PublishRequest, PublishResult>("publish", new() { IsAddon = isAddon, Title = title, Blurb = blurb, Detail = detail, Author = author, Url = url, Key = key, DiscordId = discordId, Icon = "", Langs = [] }, callback);
    }

    static public IEnumerator CoGetContent(int id, Func<ItemDetail?, IEnumerator> callback)
    {
        yield return CoPostResponse<DetailRequest, ItemDetail>("details", new() { Id = id }, callback);
    }

    static public IEnumerator CoEditContent(int entryId, string key, string title, string blurb, string detail, string author, string url, Func<EditResult?, IEnumerator> callback)
    {
        yield return CoPostResponse<EditRequest, EditResult>("edit", new() { Id = entryId, Key = key, Title = title, Blurb = blurb, Detail = detail, Author = author, Url = url, Icon = "", Langs = [] }, callback);
    }
}
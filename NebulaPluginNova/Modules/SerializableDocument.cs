using Il2CppSystem.Xml.Schema;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using Nebula.Scripts;
using System.Reflection;
using TMPro;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Compat;
using Virial.Game;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;
using Virial.Utilities;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public class DocumentManager
{
    private static Dictionary<string, IDocument> allDocuments = new();
    static public IDocument? GetDocument(string id)
    {
        if(allDocuments.TryGetValue(id, out var document)) return document;
        return null;
    }

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Serializable Documents");

        
        string Postfix = ".json";
        int PostfixLength = Postfix.Length;

        foreach (var addon in NebulaAddon.AllAddons)
        {
            string Prefix = addon.InZipPath + "Documents/";
            int PrefixLength = Prefix.Length;

            foreach (var entry in addon.Archive.Entries)
            {
                if (!entry.FullName.StartsWith(Prefix) || !entry.FullName.EndsWith(Postfix)) continue;

                var id = entry.FullName.Substring(PrefixLength, entry.FullName.Length - PrefixLength - PostfixLength).Replace('/', '.');

                using var stream = entry.Open();
                if (stream == null) continue;

                var doc = JsonStructure.Deserialize<SerializableDocument>(stream);
                if (doc == null) continue;

                foreach(var c in doc.AllConents())c.RelatedNamespace = addon;

                doc.DocumentId = id;
                allDocuments[id] = doc;
            }

            yield return null;
        }

        foreach (var assembly in AddonScriptManager.ScriptAssemblies.Select(s => s.Assembly))
        {
            var types = assembly?.GetTypes().Where((type) => type.IsAssignableTo(typeof(IDocument)) && type.GetCustomAttributes<AddonDocumentAttribute>().Any(_ => true));
            foreach (var type in types ?? [])
            {
                foreach (var attr in type.GetCustomAttributes<AddonDocumentAttribute>())
                {
                    var doc = type.GetConstructor(attr.Arguments.Select(a => a.GetType()).ToArray())?.Invoke(attr.Arguments) as IDocument;
                    if (doc is IDocumentWithId idwi) idwi.OnSetId(attr.DocumentId);
                    if (doc != null) allDocuments[attr.DocumentId] = doc;
                }
            }
        }
    }

    public static void Register(string documentId, IDocument document)
    {
        allDocuments[documentId] = document;
    }

    //ゲーム内で使用しているID
    public static IEnumerable<string> GetAllUsingId()
    {
        foreach (var role in Roles.Roles.AllRoles) yield return "role." + role.InternalName;
        foreach (var modifier in Roles.Roles.AllModifiers) yield return "role." + modifier.InternalName;
        foreach (var option in ConfigurationValues.AllEntries) yield return option.Name + ".detail";
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class SerializableDocument : IDocument
{
    public class DocumentReference
    {
        [JsonSerializableField]
        public string Id = null!;
        [JsonSerializableField]
        public Dictionary<string,string>? Arguments;
    }

    public class SerializableColor
    {
        [JsonSerializableField(true)]
        public byte? R;
        [JsonSerializableField(true)]
        public byte? G;
        [JsonSerializableField(true)]
        public byte? B;
        [JsonSerializableField(true)]
        public byte? A;
        [JsonSerializableField(true)]
        public string? Style = null;
        public Color AsColor(FunctionalEnvironment? table) => GetColor(Style != null ? table.GetString(Style) : null) ?? new Color((float)(R ?? 255) / 255f, (float)(G ?? 255) / 255f, (float)(B ?? 255) / 255f, (float)(A ?? 255) / 255f);
    }

    private static Dictionary<string, TextAttribute> TextStyle = new();
    private static Dictionary<string, Color> ColorStyle = new();

    public static void RegisterColor(string style, Color color) => ColorStyle[style] = color;
    private static Color? GetColor(string? style)
    {
        if (style == null) return null;
        if (ColorStyle.TryGetValue(style, out var col)) return col;
        return null;
    }

    static SerializableDocument()
    {
        TextStyle["standard"] = GUI.API.GetAttribute(AttributeAsset.DocumentStandard);
        TextStyle["bold"] = GUI.API.GetAttribute(AttributeAsset.DocumentBold);
        TextStyle["content"] = GUI.API.GetAttribute(AttributeAsset.DocumentStandard);
        TextStyle["title"] = GUI.API.GetAttribute(AttributeAsset.DocumentTitle);
        TextStyle["button"] = GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed);
        TextStyle["buttonlarge"] = GUI.API.GetAttribute(AttributeAsset.StandardLargeWideMasked);
        TextStyle["buttonmedium"] = GUI.API.GetAttribute(AttributeAsset.StandardMediumMasked);
    }

    public static TextAttribute GetAttribute(string style) => TextStyle.TryGetValue(style.ToLower(), out var result) ? result : TextStyle["standard"];

    public IEnumerable<SerializableDocument> AllConents()
    {
        yield return this;
        if (Contents != null) foreach (var doc in Contents) foreach (var c in doc.AllConents()) yield return c;
        if (Aligned != null) foreach (var doc in Aligned) foreach (var c in doc.AllConents()) yield return c;
    }

    //使用している引数(任意)
    [JsonSerializableField(true)]
    public List<string>? Arguments = null;

    //子となるコンテンツ
    [JsonSerializableField(true)]
    public List<SerializableDocument>? Contents = null;

    //横並びのコンテンツ
    [JsonSerializableField(true)]
    public List<SerializableDocument>? Aligned = null;

    //表示条件
    [JsonSerializableField(true)]
    public string? Predicate = null;

    //テンプレートスタイルID
    [JsonSerializableField(true)]
    public string? Style = null;

    //テキストの生文字列
    [JsonSerializableField(true)]
    public string? RawText;

    //テキストの翻訳キー
    [JsonSerializableField(true)]
    public string? TranslationKey;

    //太字
    [JsonSerializableField(true)]
    public bool? IsBold = null;

    //テキストカラー
    [JsonSerializableField(true)]
    public SerializableColor? Color = null;

    //フォントサイズ
    [JsonSerializableField(true)]
    public float? FontSize = null;

    //可変テキスト
    [JsonSerializableField(true)]
    public bool? IsVariable = null;

    //画像パス
    [JsonSerializableField(true)]
    public string? Image = null;

    //横幅
    [JsonSerializableField(true)]
    public float? Width = null;

    //縦幅
    [JsonSerializableField(true)]
    public float? Height = null;

    //縦方向余白
    [JsonSerializableField(true)]
    public float? VSpace = null;

    //横方向余白
    [JsonSerializableField(true)]
    public float? HSpace = null;

    //引用
    [JsonSerializableField(true)]
    public string? Citation = null;

    //ドキュメント参照
    [JsonSerializableField(true)]
    public DocumentReference? Document = null;

    //アラインメント
    [JsonSerializableField(true)]
    public string? Alignment = null;

    public string DocumentId { get; set; } = "Unknown";
    public IMetaWidgetOld.AlignmentOption GetAlignmentOld()
    {
        if (Alignment == null) return IMetaWidgetOld.AlignmentOption.Left;

        switch (Alignment)
        {
            case "Center":
                return IMetaWidgetOld.AlignmentOption.Center;
            case "Left":
                return IMetaWidgetOld.AlignmentOption.Left;
            case "Right":
                return IMetaWidgetOld.AlignmentOption.Right;
        }
        return IMetaWidgetOld.AlignmentOption.Left;
    }

    public GUIAlignment GetAlignment()
    {
        if (Alignment == null) return GUIAlignment.Left;

        switch (Alignment)
        {
            case "Center":
                return GUIAlignment.Center;
            case "Left":
                return GUIAlignment.Left;
            case "Right":
                return GUIAlignment.Right;
            case "Top":
                return GUIAlignment.Top;
            case "Bottom":
                return GUIAlignment.Bottom;
        }
        return GUIAlignment.Left;
    }

    public Virial.Text.TextAlignment GetTextAlignment()
    {
        if (Alignment == null) return Virial.Text.TextAlignment.Left;

        switch (Alignment)
        {
            case "Center":
            case "Bottom":
            case "Top":
                return Virial.Text.TextAlignment.Center;
            case "Left":
                return Virial.Text.TextAlignment.Left;
            case "Right":
                return Virial.Text.TextAlignment.Right;
        }
        return Virial.Text.TextAlignment.Left;
    }

    public IResourceAllocator? RelatedNamespace = null;

    private Image? imageLoader = null;
    private string? lastImagePath;

    public List<SerializableDocument>? MyContainer => Contents ?? Aligned;
    public void ReplaceContent(SerializableDocument content, bool moveToHead)
    {
        List<SerializableDocument>? list = MyContainer;
        if (list == null) return;

        int index = list.IndexOf(content);
        if (index == -1) return;

        index += moveToHead ? -1 : 1;

        if (0 <= index && index < list.Count)
        {
            list.Remove(content);
            list.Insert(index, content);
        }
    }

    public void RemoveContent(SerializableDocument content)
    {
        MyContainer?.Remove(content);
    }

    public void AppendContent(SerializableDocument content)
    {
        MyContainer?.Add(content);
    }

    private const int MaxNesting = 32;

    public Virial.Media.GUIWidget? BuildForDev(Action<PassiveButton,SerializableDocument, SerializableDocument?> editorBuilder, SerializableDocument? parent = null, IResourceAllocator? nameSpace = null)
    {
        var widget = BuildInternal(nameSpace ?? RelatedNamespace, null, null, c => c.BuildForDev(editorBuilder, this, nameSpace ?? RelatedNamespace), false, true, MaxNesting);

        if (widget != null) widget = new NoSGUIFramed(GetAlignment(), widget, new(0.15f, 0.15f)) { 
            PostBuilder = renderer =>
            {
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var button = renderer.gameObject.SetUpButton(true, renderer, UnityEngine.Color.white.AlphaMultiplied(0.15f), UnityEngine.Color.Lerp(UnityEngine.Color.cyan, UnityEngine.Color.green, 0.4f).AlphaMultiplied(0.3f));
                var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                collider.size = renderer.size;
                editorBuilder.Invoke(button,this,parent);
            } };
        
        return widget;
    }

    Virial.Media.GUIWidget? IDocument.Build(Virial.Compat.Artifact<Virial.Media.GUIScreen>? target) => Build(target);
    public Virial.Media.GUIWidget? Build(Artifact<GUIScreen>? myScreen, bool useMaskedMaterial = true, int leftNesting = MaxNesting, IResourceAllocator? nameSpace = null) => BuildInternal(nameSpace ?? RelatedNamespace, null, myScreen, c => c.Build(myScreen, useMaskedMaterial, leftNesting, nameSpace ?? RelatedNamespace), true, useMaskedMaterial, leftNesting);
    public Virial.Media.GUIWidget? BuildReference(FunctionalEnvironment? table, IResourceAllocator? nameSpace, Artifact<GUIScreen>? myScreen, bool buildHyperLink, int leftNesting = MaxNesting) => BuildInternal(nameSpace, table, myScreen, c => c.BuildReference(table, c.RelatedNamespace, myScreen, buildHyperLink, leftNesting), buildHyperLink, true, leftNesting);


    public Virial.Media.GUIWidget? BuildInternal(IResourceAllocator? nameSpace, FunctionalEnvironment? arguments, Artifact<GUIScreen>? myScreen, Func<SerializableDocument, Virial.Media.GUIWidget?> builder, bool buildHyperLink,bool useMaskedMaterial, int leftNesting)
    {
        arguments ??= new();
        arguments.TryRegister("documentId", () => IFunctionalVariable.Generate(DocumentId));

        if (Predicate != null && Predicate.Length > 0)
        {
            if (!(arguments?.GetValue(Predicate[0] is '#' ? Predicate.Substring(1) : Predicate).AsBool() ?? true))
                return new NoSGUIMargin(GetAlignment(), new(0f,0f));
        }

        string ConsiderArgumentAsStr(string str) => arguments.GetString(str);
        IFunctionalVariable ConsiderArgument(string str) => arguments.GetValueOrRaw(str);

        if (Contents != null)
            return new VerticalWidgetsHolder(GetAlignment(), Contents.Select(c => builder.Invoke(c)).Where(c => c != null)) { FixedWidth = Width };
        

        if(Aligned != null)
            return new HorizontalWidgetsHolder(GetAlignment(), Aligned.Select(c => builder.Invoke(c)).Where(c => c != null)) { FixedHeight = Height };


        if(TranslationKey != null || RawText != null)
        {
            TextComponent text = TranslationKey != null ? new TranslateTextComponent(ConsiderArgumentAsStr(TranslationKey!)) : new RawTextComponent(ConsiderArgumentAsStr(RawText!));

            TextAttribute? attr = GetAttribute(ConsiderArgumentAsStr(Style ?? ""));

            float fontSize = FontSize.HasValue ? FontSize.Value : attr.FontSize.FontSizeDefault;
            attr = new(attr) {
                FontSize = new Virial.Text.FontSize(fontSize, Mathf.Min(fontSize, attr.FontSize.FontSizeMin), Mathf.Max(fontSize, attr.FontSize.FontSizeMax)),
                Color = new(Color?.AsColor(arguments) ?? UnityEngine.Color.white),
                Style = IsBold.HasValue ? (IsBold.Value ? Virial.Text.FontStyle.Bold : Virial.Text.FontStyle.Normal) : attr.Style,
                Alignment = GetTextAlignment(),
                IsFlexible = IsVariable ?? true
            };

            void PostBuilder(TMPro.TextMeshPro text) {
                if (myScreen != null && buildHyperLink)
                {
                    foreach (var linkInfo in text.textInfo.linkInfo)
                    {
                        int begin = linkInfo.linkTextfirstCharacterIndex;
                        for (int i = 0; i < linkInfo.linkTextLength; i++)
                        {
                            int index = begin + i;
                            text.textInfo.characterInfo[i].color = new Color32(116, 132, 169, 255);
                        }
                    }

                    var collider = UnityHelper.CreateObject<BoxCollider2D>("TextCollider", text.transform, UnityEngine.Vector3.zero);
                    collider.size = text.rectTransform.sizeDelta;
                    var button = collider.gameObject.SetUpButton();
                    button.OnClick.AddListener(() =>
                    {
                        var cam = UnityHelper.FindCamera(LayerExpansion.GetUILayer());
                        if (cam == null) return;

                        int linkIdx = TMP_TextUtilities.FindIntersectingLink(text, Input.mousePosition, cam);
                        if (linkIdx == -1) return;

                        var action = text.textInfo.linkInfo[linkIdx].GetLinkID();
                        var args = action.Split(':', 2);
                        if (args.Length != 2) return;

                        switch (args[0])
                        {
                            case "to":
                                myScreen?.Do(screen => screen.SetWidget(DocumentManager.GetDocument(args[1])?.Build(myScreen) ?? null, out _));
                                break;
                            default:
                                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Document, $"Unknown link action \"{args[0]}\" is triggered.");
                                break;
                        }
                    });
                }
            }

            return new NoSGUIText(GetAlignment(), attr, text) { PostBuilder = PostBuilder };            
        }

        if(Image != null)
        {
            string image = ConsiderArgumentAsStr(Image);

            if (imageLoader == null || image != lastImagePath)
            {
                imageLoader = NebulaResourceManager.GetResource(image, nameSpace)?.AsImage();
                lastImagePath = image;
            }

            Sprite sprite = null!;
            try
            {
                sprite = imageLoader?.GetSprite()!;
            }
            catch { }



            if (sprite)
                return new NoSGUIImage(GetAlignment(), imageLoader!, new(Width, Height));
            else
                return new NoSGUIText(GetAlignment(), GUI.API.GetAttribute(AttributeAsset.StandardMediumMasked), new RawTextComponent(lastImagePath.Color(UnityEngine.Color.gray)));
        }

        if (HSpace != null || VSpace != null) return new NoSGUIMargin(GetAlignment(), new(HSpace ?? 0f, VSpace ?? 0f));

        if(Document != null)
        {
            if (leftNesting == 0)
                return new NoSGUIText(GetAlignment(), GUI.API.GetAttribute(AttributeAsset.StandardMediumMasked), GUI.Instance.TextComponent(UnityEngine.Color.red, "ui.document.tooLongNesting"));
            else
            {
                SerializableDocument? doc = null;

                string docId = ConsiderArgumentAsStr(Document.Id);
                if (nameSpace is DevAddon addon)
                {
                    string path = "Documents/" + docId + ".json";
                    var stream = nameSpace.GetResource(new ReadOnlyArray<string>(Array.Empty<string>()), path)?.AsStream();
                    if (stream != null) {
                        doc = JsonStructure.Deserialize<SerializableDocument>(new StreamReader(stream).ReadToEnd());
                    }
                }
                doc ??= DocumentManager.GetDocument(docId) as SerializableDocument;
                return doc?.BuildReference(new FunctionalEnvironment(Document.Arguments, arguments), nameSpace, myScreen, buildHyperLink, leftNesting - 1) ?? new NoSGUIMargin(GetAlignment(), UnityEngine.Vector2.zero);
            }
        }

        if(Citation != null)
        {
            var citation = ConsiderArgument(Citation).AsObject<Citation>();
            if (citation == null) Virial.Assignable.Citation.TryGetCitation(ConsiderArgumentAsStr(Citation), out citation);

            if(citation == null) return new NoSGUIMargin(GetAlignment(), UnityEngine.Vector2.zero);

            GUIClickableAction? onClick = (buildHyperLink && citation.RelatedUrl != null) ? _ => Application.OpenURL(Helpers.ConvertUrl(citation.RelatedUrl)) : null;
            var overlay = (buildHyperLink && citation.RelatedUrl != null) ? GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.citation.openUrl") : null;

            if (citation?.LogoImage != null) return GUI.Instance.Image(GetAlignment(), citation.LogoImage, new(1.5f, 0.37f), onClick, overlay);
            
            return new NoSGUIText(GetAlignment(), GUI.Instance.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), citation!.Name) {
                OverlayWidget = overlay,
                OnClickText = onClick != null ? (() => onClick?.Invoke(null!), false) : null
            };
            
        }

        //無効なコンテンツ
        return null;
    }
}

static public class DocumentTipManager
{
    static private OrderedList<WinConditionTip, int> endConditionList = OrderedList<WinConditionTip, int>.AscendingList(end => end.End.Id);
    static public IEnumerable<WinConditionTip> WinConditionTips => endConditionList.Where(e => e.IsActive);
    public static void Register(IDocumentTip tip)
    {
        if(tip is WinConditionTip wct)
            endConditionList.Add(wct);
    }
}
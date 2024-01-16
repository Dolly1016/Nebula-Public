using Il2CppSystem.Text.Json;
using Nebula.Behaviour;
using Nebula.Modules.MetaContext;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Nebula.Modules;

[NebulaPreLoad(typeof(SerializableDocument),typeof(NebulaAddon))]
public class DocumentManager
{
    private static Dictionary<string, SerializableDocument> allDocuments = new();
    static public SerializableDocument? GetDocument(string id)
    {
        if(allDocuments.TryGetValue(id, out var document)) return document;
        return null;
    }

    public static IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Loading Serializable Documents";
        yield return null;

        
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

                allDocuments[id] = doc;
            }

            yield return null;
        }
    }

    //ゲーム内で使用しているID
    public static IEnumerable<string> GetAllUsingId()
    {
        foreach (var role in Roles.Roles.AllRoles) yield return "role." + role.InternalName;
        foreach (var modifier in Roles.Roles.AllModifiers) yield return "role." + modifier.InternalName;
        foreach (var option in NebulaConfiguration.AllConfigurations) yield return option.Id + ".detail";
    }
}

[NebulaPreLoad]
public class SerializableDocument
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

    public static void Load()
    {
        TextStyle["Standard"] = new TextAttribute(TextAttribute.NormalAttr).EditFontSize(1.2f, 0.6f, 1.2f);
        TextStyle["Bold"] = new TextAttribute(TextAttribute.BoldAttr).EditFontSize(1.2f, 0.6f, 1.2f);
        TextStyle["Content"] = new TextAttribute(TextAttribute.ContentAttr).EditFontSize(1.2f, 0.6f, 1.2f);
        TextStyle["Title"] = new TextAttribute(TextAttribute.TitleAttr).EditFontSize(2.2f, 0.6f, 2.2f);
    }

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

    //縦方向余白
    [JsonSerializableField(true)]
    public float? VSpace = null;

    //横方向余白
    [JsonSerializableField(true)]
    public float? HSpace = null;

    //ドキュメント参照
    [JsonSerializableField(true)]
    public DocumentReference? Document = null;

    //アラインメント(非推奨)
    [JsonSerializableField(true)]
    public string? Alignment = null;
    public IMetaContextOld.AlignmentOption GetAlignment()
    {
        if (Alignment == null) return IMetaContextOld.AlignmentOption.Left;

        switch (Alignment)
        {
            case "Center":
                return IMetaContextOld.AlignmentOption.Center;
            case "Left":
                return IMetaContextOld.AlignmentOption.Left;
            case "Right":
                return IMetaContextOld.AlignmentOption.Right;
        }
        return IMetaContextOld.AlignmentOption.Left;
    }

    public TMPro.TextAlignmentOptions GetTextAlignment()
    {
        if (Alignment == null) return TextAlignmentOptions.Left;

        switch (Alignment)
        {
            case "Center":
                return TextAlignmentOptions.Center;
            case "Left":
                return TextAlignmentOptions.Left;
            case "Right":
                return TextAlignmentOptions.Right;
        }
        return TextAlignmentOptions.Left;
    }

    public INameSpace? RelatedNamespace = null;

    private ISpriteLoader? imageLoader = null;
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

    public IMetaContextOld? BuildForDev(Action<PassiveButton,SerializableDocument, SerializableDocument?> editorBuilder, SerializableDocument? parent = null, INameSpace? nameSpace = null)
    {
        var context = BuildInternal(nameSpace ?? RelatedNamespace, null, null, c => c.BuildForDev(editorBuilder, this, nameSpace ?? RelatedNamespace), false, true, MaxNesting);

        if (context != null) context = new MetaContextOld.FramedContext(context, new Vector2(0.15f, 0.15f)) { 
            HighlightColor = UnityEngine.Color.cyan.AlphaMultiplied(0.25f), 
            PostBuilder = renderer =>
            {
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var button = renderer.gameObject.SetUpButton(true, renderer, UnityEngine.Color.white.AlphaMultiplied(0.15f), UnityEngine.Color.Lerp(UnityEngine.Color.cyan, UnityEngine.Color.green, 0.4f).AlphaMultiplied(0.3f));
                var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                collider.size = renderer.size;
                editorBuilder.Invoke(button,this,parent);
            } };

        return context;
    }

    public IMetaContextOld? Build(Reference<MetaContextOld.ScrollView.InnerScreen>? myScreen, bool useMaskedMaterial = true, int leftNesting = MaxNesting, INameSpace? nameSpace = null) => BuildInternal(nameSpace ?? RelatedNamespace, null, myScreen, c => c.Build(myScreen, useMaskedMaterial, leftNesting, nameSpace ?? RelatedNamespace), true, useMaskedMaterial, leftNesting);
    public IMetaContextOld? BuildReference(FunctionalEnvironment? table, INameSpace? nameSpace, Reference<MetaContextOld.ScrollView.InnerScreen>? myScreen, bool buildHyperLink, int leftNesting = MaxNesting) => BuildInternal(nameSpace, table, myScreen, c => c.BuildReference(table, c.RelatedNamespace, myScreen, buildHyperLink, leftNesting), buildHyperLink, true, leftNesting);


    public IMetaContextOld? BuildInternal(INameSpace? nameSpace, FunctionalEnvironment? arguments, Reference<MetaContextOld.ScrollView.InnerScreen>? myScreen, Func<SerializableDocument, IMetaContextOld?> builder, bool buildHyperLink,bool useMaskedMaterial, int leftNesting)
    {
        if(Predicate != null && Predicate.Length > 0)
        {
            if (!(arguments?.GetValue(Predicate[0] is '#' ? Predicate.Substring(1) : Predicate).AsBool() ?? true))
                return new MetaContextOld();
        }

        string ConsiderArgumentAsStr(string str) => arguments.GetString(str);

        if (Contents != null)
        {
            MetaContextOld context = new();
            context.MaxWidth = Width;

            foreach(var c in Contents)
            {
                var subContext = builder.Invoke(c);
                if (subContext != null) context.Append(subContext);
            }
            return context;
        }

        if(Aligned != null)
        {
            List<IMetaParallelPlacableOld> list = new();
            foreach (var c in Aligned)
            {
                var tem = builder.Invoke(c);
                if (!(tem is IMetaParallelPlacableOld mpp))
                {
                    NebulaPlugin.Log.Print(NebulaLog.LogCategory.Document,"Document contains an unalignable content.");
                    continue;
                }
                list.Add(mpp);
            }
            return new CombinedContextOld(list.ToArray()) { Alignment = GetAlignment() };
        }

        if(TranslationKey != null || RawText != null)
        {
            string text = TranslationKey != null ? Language.Translate(ConsiderArgumentAsStr(TranslationKey!)) : ConsiderArgumentAsStr(RawText!);

            TextAttribute? attr = null;
            if(Style == null || !TextStyle.TryGetValue(ConsiderArgumentAsStr(Style), out attr)) attr = (IsVariable ?? false) ? TextStyle["Content"] : TextStyle["Standard"];

            float fontSize = FontSize.HasValue ? FontSize.Value : attr.FontSize;
            attr = new(attr) {
                FontSize = fontSize,
                FontMinSize = Mathf.Min(fontSize, attr.FontMinSize),
                FontMaxSize = Mathf.Max(fontSize, attr.FontMaxSize),
                Color = Color?.AsColor(arguments) ?? UnityEngine.Color.white,
                Styles = IsBold.HasValue ? (IsBold.Value ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal) : attr.Styles,
                Alignment = GetTextAlignment(),
                FontMaterial = useMaskedMaterial ? VanillaAsset.StandardMaskedFontMaterial : null
               
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

                    var collider = UnityHelper.CreateObject<BoxCollider2D>("TextCollider", text.transform.parent, text.transform.localPosition);
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
                                myScreen?.Value?.SetContext(DocumentManager.GetDocument(args[1])?.Build(myScreen) ?? null);
                                break;
                            default:
                                NebulaPlugin.Log.Print(NebulaLog.LogCategory.Document, $"Unknown link action \"{args[0]}\" is triggered.");
                                break;
                        }
                    });
                }
            }

            if (IsVariable ?? false)
            {
                return new MetaContextOld.VariableText(attr) { RawText = text, Alignment = GetAlignment(), PostBuilder =  PostBuilder };
            }
            else
            {
                return new MetaContextOld.Text(attr) { RawText = text, Alignment = GetAlignment(), PostBuilder = PostBuilder };
            }
        }

        if(Image != null)
        {
            string image = ConsiderArgumentAsStr(Image);
            if (imageLoader == null || image != lastImagePath)
            {
                if (image.Contains("::"))
                {
                    var splitted = image.Split("::", 2);
                    imageLoader = NameSpaceManager.ResolveOrGetDefault(splitted[0]).GetSprite(splitted[1], 100f);
                }
                else
                {
                    imageLoader = (nameSpace ?? NameSpaceManager.DefaultNameSpace).GetSprite(image, 100f);
                }
                lastImagePath = image;
            }

            Sprite sprite = null!;
            try
            {
                sprite = imageLoader?.GetSprite()!;
            }
            catch { }
            if (sprite)
                return new MetaContextOld.Image(sprite) { Width = Width ?? 1f, PostBuilder = image => image.maskInteraction = useMaskedMaterial ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None, Alignment = GetAlignment() };
            else
                return new MetaContextOld.VariableText(new TextAttribute(TextAttribute.BoldAttrLeft) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(1.4f)) { RawText = lastImagePath.Color(UnityEngine.Color.gray), Alignment = GetAlignment() };
        }

        if (HSpace != null) return new MetaContextOld.HorizonalMargin(HSpace.Value);
        if (VSpace != null) return new MetaContextOld.VerticalMargin(VSpace.Value);

        if(Document != null)
        {
            if (leftNesting == 0)
            {
                return new MetaContextOld.VariableText(new TextAttribute(TextAttribute.BoldAttrLeft) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(1f)) { MyText = NebulaGUIContextEngine.Instance.TextComponent(UnityEngine.Color.red, "ui.document.tooLongNesting"), Alignment = IMetaContextOld.AlignmentOption.Left };
            }
            else
            {
                SerializableDocument? doc = null;

                if (nameSpace is DevAddon addon)
                {
                    string path = "Documents/" + Document.Id + ".json";
                    var stream = nameSpace?.OpenRead(path);
                    if (stream != null) {
                        doc = JsonStructure.Deserialize<SerializableDocument>(new StreamReader(stream).ReadToEnd());
                    }
                }
                doc ??= DocumentManager.GetDocument(ConsiderArgumentAsStr(Document.Id));
                return doc?.BuildReference(new FunctionalEnvironment(Document.Arguments, arguments), nameSpace, myScreen, buildHyperLink, leftNesting - 1) ?? new MetaContextOld();
            }
        }
        //無効なコンテンツ
        return null;
    }
}

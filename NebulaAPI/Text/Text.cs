using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using Virial.Compat;

namespace Virial.Text;

public enum FontAsset
{
    Gothic,
    GothicMasked,
    Oblong,
    OblongMasked,
    Prespawn,
    Barlow
}

public enum TextAlignment
{
    Left = TMPro.TextAlignmentOptions.Left,
    Right = TMPro.TextAlignmentOptions.Right,
    Center = TMPro.TextAlignmentOptions.Center,
    TopLeft = TMPro.TextAlignmentOptions.TopLeft,
    TopRight = TMPro.TextAlignmentOptions.TopRight,
    Top = TMPro.TextAlignmentOptions.Top,
    BottomLeft = TMPro.TextAlignmentOptions.BottomLeft,
    BottomRight = TMPro.TextAlignmentOptions.BottomRight,
    Bottom = TMPro.TextAlignmentOptions.Bottom,
}

[Flags]
internal enum AttributeTemplateFlag
{
    FontMask        = 0b1111,
    FontStandard    = 0b0001,
    FontOblong      = 0b0010,

    AlignmentMask   = 0b11 << 4,
    AlignmentLeft   = 0b01 << 4,
    AlignmentCenter = 0b10 << 4,
    AlignmentRight  = 0b11 << 4,

    MaterialMask    = 0b1 << 6,
    MaterialNormal  = 0b0 << 6,
    MaterialBared   = 0b1 << 6,
    
    StyleMask       = 0b1 << 7,
    StyleBold       = 0b1 << 7,

    OtherSetingMask = 0b1 << 8,
    IsFlexible      = 0b1 << 8,
}

[Flags]
public enum AttributeParams
{
    StandardBared           = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentCenter | AttributeTemplateFlag.MaterialBared   | AttributeTemplateFlag.IsFlexible,
    StandardBaredBold       = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentCenter | AttributeTemplateFlag.MaterialBared   | AttributeTemplateFlag.IsFlexible  | AttributeTemplateFlag.StyleBold, 
    StandardBaredLeft       = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentLeft   | AttributeTemplateFlag.MaterialBared   | AttributeTemplateFlag.IsFlexible,
    StandardBaredBoldLeft   = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentLeft   | AttributeTemplateFlag.MaterialBared   | AttributeTemplateFlag.IsFlexible  | AttributeTemplateFlag.StyleBold,
    Standard                = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentCenter | AttributeTemplateFlag.MaterialNormal  | AttributeTemplateFlag.IsFlexible,
    StandardBold            = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentCenter | AttributeTemplateFlag.MaterialNormal  | AttributeTemplateFlag.IsFlexible  | AttributeTemplateFlag.StyleBold,
    StandardLeft            = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentLeft   | AttributeTemplateFlag.MaterialNormal  | AttributeTemplateFlag.IsFlexible,
    StandardBoldLeft        = AttributeTemplateFlag.FontStandard    | AttributeTemplateFlag.AlignmentLeft   | AttributeTemplateFlag.MaterialNormal  | AttributeTemplateFlag.IsFlexible  | AttributeTemplateFlag.StyleBold, 

    StandardBaredNonFlexible            = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentCenter    | AttributeTemplateFlag.MaterialBared,
    StandardBaredBoldNonFlexible        = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentCenter    | AttributeTemplateFlag.MaterialBared   | AttributeTemplateFlag.StyleBold,
    StandardBaredLeftNonFlexible        = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentLeft      | AttributeTemplateFlag.MaterialBared,
    StandardBaredBoldLeftNonFlexible    = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentLeft      | AttributeTemplateFlag.MaterialBared   | AttributeTemplateFlag.StyleBold,
    StandardNonFlexible                 = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentCenter    | AttributeTemplateFlag.MaterialNormal,
    StandardBoldNonFlexible             = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentCenter    | AttributeTemplateFlag.MaterialNormal  | AttributeTemplateFlag.StyleBold,
    StandardLeftNonFlexible             = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentLeft      | AttributeTemplateFlag.MaterialNormal,
    StandardBoldLeftNonFlexible         = AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.AlignmentLeft      | AttributeTemplateFlag.MaterialNormal  | AttributeTemplateFlag.StyleBold,

    OblongBared             = AttributeTemplateFlag.FontOblong      | AttributeTemplateFlag.AlignmentCenter | AttributeTemplateFlag.MaterialBared,
    OblongBaredLeft         = AttributeTemplateFlag.FontOblong      | AttributeTemplateFlag.AlignmentLeft   | AttributeTemplateFlag.MaterialBared,
    Oblong                  = AttributeTemplateFlag.FontOblong      | AttributeTemplateFlag.AlignmentCenter | AttributeTemplateFlag.MaterialNormal,
    OblongLeft              = AttributeTemplateFlag.FontOblong      | AttributeTemplateFlag.AlignmentLeft   | AttributeTemplateFlag.MaterialNormal

    
}

public enum AttributeAsset
{
    /// <summary>
    /// タイトル画面の縦長の文字です。実績確認画面のヘッダーなどで使われています。
    /// </summary>
    OblongHeader,
    
    /// <summary>
    /// ボタン向けの固定サイズテキスト属性です。
    /// Preset読み込み画面のReloadボタン、Save As...ボタンと同じ大きさです。
    /// </summary>
    StandardMediumMasked,

    /// <summary>
    /// ボタン向けの固定サイズテキスト属性です。
    /// Preset読み込み画面の各プリセットのボタンと同じ大きさです。
    /// </summary>
    StandardLargeWideMasked,

    /// <summary>
    /// 中揃えの小見出しや注目を浴びるテキスト向けの可変サイズテキスト属性です。
    /// </summary>
    CenteredBold,

    /// <summary>
    /// 中揃えの小見出しやボタン向けの固定サイズテキスト属性です。
    /// </summary>
    CenteredBoldFixed,

    /// <summary>
    /// 左揃えの小見出しやボタン向けの固定サイズテキスト属性です。
    /// </summary>
    LeftBoldFixed,

    /// <summary>
    /// 主にオーバーレイ向けの見出し用可変サイズテキスト属性です。
    /// </summary>
    OverlayTitle,

    /// <summary>
    /// 主にオーバーレイ向けの本文用可変サイズテキスト属性です。
    /// </summary>
    OverlayContent,

    /// <summary>
    /// SerializableDocumentのTextStyle"Standard"で提供されているテキスト属性です。
    /// </summary>
    DocumentStandard,

    /// <summary>
    /// SerializableDocumentのTextStyle"Bold"で提供されているテキスト属性です。
    /// </summary>
    DocumentBold,

    /// <summary>
    /// SerializableDocumentのTextStyle"Title"で提供されているテキスト属性です。
    /// </summary>
    DocumentTitle,

    /// <summary>
    /// DocumentTitleより小さく、DocumentSubtitle2より大きいテキスト属性です。
    /// </summary>
    DocumentSubtitle1,

    /// <summary>
    /// DocumentSubtitle1より小さく、DocumentStandardより大きいテキスト属性です。
    /// </summary>
    DocumentSubtitle2,

    /// <summary>
    /// VC Settingsのデバイス設定で使用している横長のボタンです。
    /// </summary>
    DeviceButton,

    /// <summary>
    /// オプションの名称の表示で使用している固定長テキスト属性です。
    /// </summary>
    OptionsTitle,

    /// <summary>
    /// オプションの名称の表示で使用している固定長テキスト属性です。
    /// </summary>
    OptionsTitleHalf,

    /// <summary>
    /// オプションの名称の表示で使用している固定長テキスト属性です。
    /// </summary>
    OptionsTitleShortest,

    /// <summary>
    /// オプションの値の表示で使用している固定長テキスト属性です。
    /// </summary>
    OptionsValue,

    /// <summary>
    /// オプションの値の表示で使用している固定長テキスト属性です。
    /// </summary>
    OptionsValueShorter,

    /// <summary>
    /// オプション値の変更ボタンで使用している固定長テキスト属性です。
    /// </summary>
    OptionsButton,

    /// <summary>
    /// 真偽値オプションで使用されている固定長テキスト属性です。
    /// </summary>
    OptionsButtonMedium,

    /// <summary>
    /// クールダウンオプションで設定方法を変更するボタンで使用されている固定長テキスト属性です。
    /// </summary>
    OptionsButtonLonger,

    /// <summary>
    /// オプション値やオプション名と同じフォントの可変テキスト属性です。
    /// </summary>
    OptionsFlexible,

    /// <summary>
    /// グループ化されたオプションのタイトルで使用される可変長テキスト属性です。
    /// </summary>
    OptionsGroupTitle,

    /// <summary>
    /// マーケットプレイスのタイトルで使われている固定長テキスト属性です。
    /// </summary>
    MarketplaceTitle,

    /// <summary>
    /// マーケットプレイスの作者名で使われている固定長テキスト属性です。
    /// </summary>
    MarketplaceDeveloper,

    /// <summary>
    /// マーケットプレイスの見出しで使われている固定長テキスト属性です。
    /// </summary>
    MarketplaceBlurb,

    /// <summary>
    /// マーケットプレイスで四角いボタンに使われているテキスト属性です。
    /// </summary>
    MarketplacePublishButton,

    /// <summary>
    /// マーケットプレイスでアドオンとコスチュームの切り替えボタンに使われているテキスト属性です。
    /// </summary>
    MarketplaceCategoryButton,

    /// <summary>
    /// マーケットプレイスでタブボタンに使用されているテキスト属性です。
    /// </summary>
    MarketplaceTabButton,

    /// <summary>
    /// 会議中に表示される称号で使用されているテキスト属性です。
    /// </summary>
    MeetingTitle,

    /// <summary>
    /// ロビーでのバージョン表示で使用されているテキスト属性です。
    /// </summary>
    VersionShower,

    /// <summary>
    /// フリープレイの役職選択ボタンで使用されているテキスト属性です。
    /// </summary>
    MetaRoleButton,

    /// <summary>
    /// 配役タブの人数設定で使用している横長の小さなテキスト属性です。
    /// </summary>
    SmallWideButton,

    /// <summary>
    /// 配役タブの人数設定で使用している小さな矢印のためのテキスト属性です。
    /// </summary>
    SmallArrowButton,
}

[Flags]
public enum FontStyle
{
    Normal = TMPro.FontStyles.Normal,
    Bold = TMPro.FontStyles.Bold,
    Italic = TMPro.FontStyles.Italic
}

public interface Font
{
    internal Material? FontMaterial { get; }
    internal TMPro.TMP_FontAsset Font { get; }
}

internal class StaticFont : Font
{
    public Material? FontMaterial { get; init; }

    public TMP_FontAsset Font { get; init; }

    public StaticFont(Material? material, TMP_FontAsset font)
    {
        FontMaterial = material;
        Font = font;
    }
}

internal class DynamicFont : Font
{
    public Material? FontMaterial => FontMaterialSupplier.Invoke();

    public TMP_FontAsset Font => FontSupplier.Invoke();

    public Func<Material?> FontMaterialSupplier { get; init; }
    public Func<TMP_FontAsset> FontSupplier { get; init; }

    public DynamicFont(Func<Material?> material, Func<TMP_FontAsset> font)
    {
        FontMaterialSupplier = material;
        FontSupplier = font;
    }
}

public class FontSize
{
    internal float FontSizeDefault { get; private init; }
    internal float FontSizeMin { get; private init; }
    internal float FontSizeMax { get; private init; }
    internal bool AllowAutoSizing { get; private init; }

    public FontSize(float fontSize, float fontSizeMin, float fontSizeMax, bool allowAutoSizing = true)
    {
        FontSizeDefault = fontSize;
        FontSizeMin = fontSizeMin;
        FontSizeMax = fontSizeMax;
        AllowAutoSizing = allowAutoSizing;
    }

    public FontSize(float fontSize, bool allowAutoSizing = true) : this(fontSize, fontSize, fontSize, allowAutoSizing) { }
}

public class TextAttribute
{
    public TextAlignment Alignment { get; init; }
    public Font Font { get; init; }
    public FontStyle Style { get; init; }
    public FontSize FontSize { get; init; }
    public Size Size { get; init; }
    public Color Color { get; init; }
    public bool IsFlexible { get; init; }
    public bool Wrapping { get; init; } = false;
    public float? OutlineWidth { get; init; } = null;

    public TextAttribute(TextAlignment alignment, Font font, FontStyle style, FontSize fontSize, Size size, Color color, bool isFlexible, float? outlineWidth = null)
    {
        Alignment = alignment;
        Font = font;
        Style = style;
        FontSize = fontSize;
        Size = size;
        Color = color;
        IsFlexible = isFlexible;
        OutlineWidth = outlineWidth;
    }

    public TextAttribute(TextAttribute original)
    {
        Alignment = original.Alignment;
        Font = original.Font;
        Style = original.Style;
        FontSize = original.FontSize;
        Size = original.Size;
        Color = original.Color;
        IsFlexible = original.IsFlexible;
        OutlineWidth = original.OutlineWidth;
    }

    public static implicit operator TextAttribute(AttributeParams param) => NebulaAPI.GUI.GetAttribute(param);
    public static implicit operator TextAttribute(AttributeAsset asset) => NebulaAPI.GUI.GetAttribute(asset);
}

public interface TextComponent
{
    string GetString();
    string TextForCompare => GetString();
}

public static class TextComponentHelper
{
    public static TextComponent Color(this TextComponent component, Virial.Color color) => NebulaAPI.GUI.ColorTextComponent(color, component);
    public static TextComponent Size(this TextComponent component, float size) => NebulaAPI.GUI.SizedTextComponent(size, component);
    public static TextComponent Lines(params TextComponent[] components) => NebulaAPI.GUI.FunctionalTextComponent(() => string.Join("<br>", components.Select(c => c.GetString())));
    public static TextComponent Italic(this TextComponent component) => NebulaAPI.GUI.ItalicTextComponent(component);
    public static TextComponent Bold(this TextComponent component) => NebulaAPI.GUI.BoldTextComponent(component);
}

public class CombinedTextComponent : TextComponent
{
    private TextComponent[] components;
    public string GetString() => string.Join<string>("", components.Select(c => c.GetString()));

    public CombinedTextComponent(params TextComponent[] components)
    {
        this.components = components;
    }
}


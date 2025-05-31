using UnityEngine;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Compat;
using Virial.Text;

namespace Virial.Media;

/// <summary>
/// スクリーン上に表示されるオブジェクトの配置位置を指定します。
/// </summary>
[Flags]
public enum GUIAlignment
{
    /// <summary>
    /// 中央に配置します。
    /// </summary>
    Center  = 0b0000,
    /// <summary>
    /// 左側に寄せて配置します。
    /// </summary>
    Left    = 0b0001,
    /// <summary>
    /// 右側に寄せて配置します。
    /// </summary>
    Right   = 0b0010,
    /// <summary>
    /// 下方に寄せて配置します。
    /// </summary>
    Bottom  = 0b0100,
    /// <summary>
    /// 上方に寄せて配置します。
    /// </summary>
    Top     = 0b1000,

    /// <summary>
    /// 上方左側に寄せて配置します。
    /// </summary>
    TopLeft     = Left | Top,
    /// <summary>
    /// 上方右側に寄せて配置します。
    /// </summary>
    TopRight    = Right | Top,
    /// <summary>
    /// 下方左側に寄せて配置します。
    /// </summary>
    BottomLeft  = Left | Bottom,
    /// <summary>
    /// 下方右側に寄せて配置します。
    /// </summary>
    BottomRight = Right | Bottom,
}

/// <summary>
/// 画面の基準点を表します。
/// 生成されたスクリーン上の点と空間上の点は同じ位置にくるように重ね合わせられます。
/// </summary>
/// <param name="pivot">重ね合わせるスクリーン上の点</param>
/// <param name="anchoredPosition">重ね合わせられる空間上の点</param>
public record Anchor(Virial.Compat.Vector2 pivot, Virial.Compat.Vector3 anchoredPosition)
{
    public static Anchor At(Compat.Vector2 pivot) => new(pivot,new(0f,0f,0f));
}

/// <summary>
/// GUIの画面を表します。
/// </summary>
public interface GUIScreen
{
    /// <summary>
    /// スクリーン上の表示を更新します。
    /// </summary>
    /// <param name="Widget">表示するウィジェット定義</param>
    /// <param name="actualSize">生成された画面の大きさ</param>
    void SetWidget(GUIWidget? Widget, out Size actualSize);
}

public delegate GUIWidget GUIWidgetSupplier();
public delegate void GUIClickableAction(GUIClickable clickable);

/// <summary>
/// GUI上に表示できるオブジェクトの定義を表します。
/// </summary>
public abstract class GUIWidget
{
    internal abstract GUIAlignment Alignment { get; }
    internal Image? BackImage { get; set; } = null!;
    internal bool GrayoutedBackImage { get; set; } = false;
    internal abstract GameObject? Instantiate(Size size, out Size actualSize);
    internal abstract GameObject? Instantiate(Anchor anchor, Size size, out Size actualSize);

    /// <summary>
    /// GUIWidgetをSupplierの形式に変換します。
    /// </summary>
    /// <param name="widget"></param>
    public static implicit operator GUIWidgetSupplier(GUIWidget? widget) => () => widget ?? NebulaAPI.GUI.EmptyWidget;
}

/// <summary>
/// GUI上のクリック可能なオブジェクトを表します。
/// </summary>
public class GUIClickable
{
    internal PassiveUiElement uiElement { get; init; }
    public IGUISelectable? Selectable { get; private init; }

    internal GUIClickable(PassiveUiElement uiElement, IGUISelectable? selectable = null)
    {
        this.uiElement = uiElement;
        this.Selectable = selectable;
    }
}

public interface IGUISelectable
{
    void Unselect();
    void Select();
    public bool IsSelected { get; }
}

/// <summary>
/// 更新可能なテキスト
/// </summary>
public interface GUIUpdatableText
{
    void UpdateText(string rawText);
    void UpdateText(TextComponent text);
}

/// <summary>
/// GUI上に表示する各種オブジェクトの定義や関連するオブジェクトを生成できます。
/// </summary>
public interface GUI
{
    /// <summary>
    /// 何も表示しないウィジェットです。
    /// </summary>
    GUIWidget EmptyWidget { get; }

    /// <summary>
    /// 生文字列のウィジェットです。
    /// Textメソッドの呼び出しを簡素化した冗長なメソッドです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="attribute">テキストの属性</param>
    /// <param name="rawText">表示する文字列</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget RawText(GUIAlignment alignment, TextAttribute attribute, string rawText);

    /// <summary>
    /// 翻訳キーに対応する文字列のウィジェットです。
    /// Textメソッドの呼び出しを簡素化した冗長なメソッドです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="attribute">テキストの属性</param>
    /// <param name="translationKey">翻訳キー</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget LocalizedText(GUIAlignment alignment, TextAttribute attribute, string translationKey);

    /// <summary>
    /// 文字列のウィジェットです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="attribute">テキストの属性</param>
    /// <param name="text">テキストを表すコンポーネント</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget Text(GUIAlignment alignment, TextAttribute attribute, TextComponent text);

    /// <summary>
    /// 変更可能なテキストのウィジェットです。
    /// </summary>
    /// <returns>生成されたウィジェット定義</returns>
    //GUIWidget Text(GUIAlignment alignment, TextAttribute attribute, TextComponent defaultText,out Artifact<GUIUpdatableText> artifact);

    /// <summary>
    /// 翻訳テキストを表示するボタンです。
    /// Buttonメソッドの呼び出しを簡素化した冗長なメソッドです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="attribute">テキストの属性</param>
    /// <param name="translationKey">翻訳キー</param>
    /// <param name="onClick">クリックされた際に実行するアクション</param>
    /// <param name="onMouseOver">カーソルが触れた際に実行するアクション</param>
    /// <param name="onMouseOut">カーソルが離れた際に実行するアクション</param>
    /// <param name="onRightClick">右クリックされた際に実行するアクション</param>
    /// <param name="color">ボタンの色</param>
    /// <param name="selectedColor">カーソルが重なっている時のボタンの色</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget LocalizedButton(GUIAlignment alignment, TextAttribute attribute, string translationKey, GUIClickableAction onClick, GUIClickableAction? onMouseOver = null, GUIClickableAction? onMouseOut = null, GUIClickableAction? onRightClick = null, Color? color = null, Color? selectedColor = null, float? margin = null);

    /// <summary>
    /// 生文字列を表示するボタンです。
    /// Buttonメソッドの呼び出しを簡素化した冗長なメソッドです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="attribute">テキストの属性</param>
    /// <param name="rawText">表示するテキスト</param>
    /// <param name="onClick">クリックされた際に実行するアクション</param>
    /// <param name="onMouseOver">カーソルが触れた際に実行するアクション</param>
    /// <param name="onMouseOut">カーソルが離れた際に実行するアクション</param>
    /// <param name="onRightClick">右クリックされた際に実行するアクション</param>
    /// <param name="color">ボタンの色</param>
    /// <param name="selectedColor">カーソルが重なっている時のボタンの色</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget RawButton(GUIAlignment alignment, TextAttribute attribute, string rawText, GUIClickableAction onClick, GUIClickableAction? onMouseOver = null, GUIClickableAction? onMouseOut = null, GUIClickableAction? onRightClick = null, Color? color = null, Color? selectedColor = null, float? margin = null);

    /// <summary>
    /// テキストを表示するボタンです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="attribute">テキストの属性</param>
    /// <param name="text">テキストを表すコンポーネント</param>
    /// <param name="onClick">クリックされた際に実行するアクション</param>
    /// <param name="onMouseOver">カーソルが触れた際に実行するアクション</param>
    /// <param name="onMouseOut">カーソルが離れた際に実行するアクション</param>
    /// <param name="onRightClick">右クリックされた際に実行するアクション</param>
    /// <param name="color">ボタンの色</param>
    /// <param name="selectedColor">カーソルが重なっている時のボタンの色</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget Button(GUIAlignment alignment, TextAttribute attribute, TextComponent text, GUIClickableAction onClick, GUIClickableAction? onMouseOver = null, GUIClickableAction? onMouseOut = null, GUIClickableAction? onRightClick = null, Color? color = null, Color? selectedColor = null, float? margin = null);

    /// <summary>
    /// 値の増減に使うことができるボタンです。
    /// </summary>
    /// <param name="alignment"></param>
    /// <param name="onClick"></param>
    /// <returns></returns>
    GUIWidget SpinButton(GUIAlignment alignment, Action<bool> onClick);

    /// <summary>
    /// 画像を表示するウィジェットです。
    /// </summary>
    /// <param name="alignment">画像の表示位置</param>
    /// <param name="image">画像</param>
    /// <param name="size">表示する大きさ</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget Image(GUIAlignment alignment, Image image, FuzzySize size, GUIClickableAction? onClick = null, GUIWidgetSupplier? overlay = null);

    /// <summary>
    /// スクロールビューです。
    /// </summary>
    /// <param name="alignment">ビューの配置位置</param>
    /// <param name="size">ビューの大きさ</param>
    /// <param name="scrollerTag">スクローラー位置を再現するためのタグ</param>
    /// <param name="inner">ビュー内で表示するウィジェット</param>
    /// <param name="artifact">ビュー内に生成されるスクリーンへのアクセサ</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget ScrollView(GUIAlignment alignment, Size size, string? scrollerTag, GUIWidget? inner, out Artifact<GUIScreen> artifact);

    /// <summary>
    /// スクローラのないビューです。動的に表示内容を変更することができる、事前に占有された領域を表します。
    /// 内部のウィジェット定義はいつでも書き換えることができます。
    /// </summary>
    /// <param name="alignment">ビューの配置位置</param>
    /// <param name="size">ビューの大きさ</param>
    /// <param name="inner">ビュー内で表示するデフォルトのウィジェット</param>
    /// <param name="artifact">ビュー内に生成されるスクリーンへのアクセサ</param>
    /// <returns></returns>
    //GUIWidget FixedView(GUIAlignment alignment, Size size, GUIWidget? inner, out Artifact<GUIScreen> artifact);

    /// <summary>
    /// 縦方向にウィジェットを並べます。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="innerReference">並べるウィジェット　GUIScreen.SetWidgetの呼び出し時に評価されます</param>
    /// <param name="fixedWidth">ウィジェットの固定幅 nullの場合はフレキシブルに幅を設定します</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget VerticalHolder(GUIAlignment alignment, IEnumerable<GUIWidget?> innerReference, float? fixedWidth = null);

    /// <summary>
    /// 横方向にウィジェットを並べます。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="innerReference">並べるウィジェット　GUIScreen.SetWidgetの呼び出し時に評価されます</param>
    /// <param name="fixedHeight">ウィジェットの固定長 nullの場合はフレキシブルに高さを設定します</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget HorizontalHolder(GUIAlignment alignment, IEnumerable<GUIWidget?> innerReference, float? fixedHeight = null);

    /// <summary>
    /// 縦方向にウィジェットを並べます。
    /// 呼び出しを簡素化するためのオーバーロードです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="fixedWidth">ウィジェットの固定幅 nullの場合はフレキシブルに幅を設定します</param>
    /// <param name="inner">並べるウィジェット</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget VerticalHolder(GUIAlignment alignment, float fixedWidth, params GUIWidget?[] inner) => VerticalHolder(alignment, inner, null);

    /// <summary>
    /// 横方向にウィジェットを並べます。
    /// 呼び出しを簡素化するためのオーバーロードです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="fixedHeight">ウィジェットの固定長 nullの場合はフレキシブルに高さを設定します</param>
    /// <param name="inner">並べるウィジェット</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget HorizontalHolder(GUIAlignment alignment, float fixedHeight, params GUIWidget?[] inner) => HorizontalHolder(alignment, inner, null);

    /// <summary>
    /// ウィジットを指定の個数ずつ縦方向に伸ばしながら配置します。
    /// </summary>
    /// <param name="alignment"></param>
    /// <param name="inner"></param>
    /// <param name="perLine"></param>
    /// <returns></returns>
    GUIWidget Arrange(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> inner, int perLine);

    /// <summary>
    /// 縦方向にウィジェットを並べます。
    /// 呼び出しを簡素化するためのオーバーロードです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="inner">並べるウィジェット</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget VerticalHolder(GUIAlignment alignment, params GUIWidget?[] inner) => VerticalHolder(alignment, inner, null);

    /// <summary>
    /// 横方向にウィジェットを並べます。
    /// 呼び出しを簡素化するためのオーバーロードです。
    /// </summary>
    /// <param name="alignment">ウィジェットの配置位置</param>
    /// <param name="inner">並べるウィジェット</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget HorizontalHolder(GUIAlignment alignment, params GUIWidget?[] inner) => HorizontalHolder(alignment, inner, null);
    
    /// <summary>
    /// 余白を表すウィジェットです。見た目を整えるために使用します。
    /// </summary>
    /// <param name="margin">余白の大きさ</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget Margin(FuzzySize margin);

    /// <summary>
    /// 余白を表すウィジェットです。
    /// Marginの呼び出しを簡素化するための冗長なメソッドです。
    /// </summary>
    /// <param name="margin">縦方向の余白</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget VerticalMargin(float margin) => Margin(new(null, margin));

    /// <summary>
    /// 余白を表すウィジェットです。
    /// Marginの呼び出しを簡素化するための冗長なメソッドです。
    /// </summary>
    /// <param name="margin">横方向の余白</param>
    /// <returns>生成されたウィジェット定義</returns>
    GUIWidget HorizontalMargin(float margin) => Margin(new(margin, null));


    /// <summary>
    /// プレイヤーを表示するウィジェットです。
    /// </summary>
    /// <param name="player"></param>
    /// <returns>生成されたウィジット定義</returns>
    //GUIWidget PlayerDisplay(Game.Player player);

    /// <summary>
    /// プレイヤーアイコンを表示するウィジェットです。
    /// </summary>
    /// <param name="playerId">プレイヤーのID NoSの場合、色IDと同一</param>
    /// <returns>生成されたウィジット定義</returns>

    //GUIWidget PlayerIcon(byte playerId);

    /// <summary>
    /// 拡大・縮小するウィジットです。
    /// </summary>
    /// <param name="scale">拡大率</param>
    /// <returns>生成されたウィジット定義</returns>
    //GUIWidget Scaler(float scale);

    /// <summary>
    /// フォントを取得します。
    /// </summary>
    /// <param name="font">フォントの識別子</param>
    /// <returns>フォント</returns>
    Text.Font GetFont(Text.FontAsset font);

    /// <summary>
    /// パラメータからテキスト属性を取得します。
    /// GenerateAttributeメソッドで同等のテキスト属性を生成することもできますが、このメソッドから取得できるテキスト属性はキャッシュされており、メモリを過剰に逼迫しません。
    /// </summary>
    /// <param name="attribute">属性のパラメータ</param>
    /// <returns>テキスト属性</returns>
    Text.TextAttribute GetAttribute(Text.AttributeParams attribute);

    /// <summary>
    /// 識別子からテキスト属性を取得します。
    /// Nebulaで用いられているテキスト属性をそのまま借用できます。
    /// GenerateAttributeメソッドで同等のテキスト属性を生成することもできますが、このメソッドから取得できるテキスト属性はキャッシュされており、メモリを過剰に逼迫しません。
    /// </summary>
    /// <param name="attribute">属性の識別子</param>
    /// <returns>テキスト属性</returns>
    Text.TextAttribute GetAttribute(Text.AttributeAsset attribute);

    /// <summary>
    /// テキスト属性を生成します。
    /// 同じ属性を何度も生成すると非効率的です。テキスト属性は不変ですので、再利用できるテキスト属性は積極的に再利用を心がけてください。
    /// </summary>
    /// <param name="attribute">属性のパラメータ</param>
    /// <param name="color">テキスト色</param>
    /// <param name="fontSize">フォントサイズ</param>
    /// <param name="size">文字列の占有する大きさの上限</param>
    /// <returns></returns>
    Text.TextAttribute GenerateAttribute(Text.AttributeParams attribute, Color color, FontSize fontSize, Size size);

    /// <summary>
    /// 色付き翻訳テキストコンポーネントを生成します。
    /// 呼び出しを簡素化するための冗長なメソッドです。
    /// </summary>
    /// <param name="color">テキスト色</param>
    /// <param name="transrationKey">翻訳キー</param>
    /// <returns>テキストコンポーネント</returns>
    TextComponent TextComponent(Virial.Color color, string transrationKey);

    /// <summary>
    /// 生文字列のテキストコンポーネントを生成します。
    /// </summary>
    /// <param name="rawText">表すテキスト</param>
    /// <returns>テキストコンポーネント</returns>
    TextComponent RawTextComponent(string rawText);

    /// <summary>
    /// 翻訳テキストのコンポーネントを生成します。
    /// </summary>
    /// <param name="translationKey">翻訳キー</param>
    /// <returns></returns>
    TextComponent LocalizedTextComponent(string translationKey);

    /// <summary>
    /// 色付きテキストのコンポーネントを生成します。
    /// </summary>
    /// <param name="color">テキスト色</param>
    /// <param name="component">テキストコンポーネント</param>
    /// <returns>色付きのテキストコンポーネント</returns>
    TextComponent ColorTextComponent(Virial.Color color, TextComponent component);

    TextComponent SizedTextComponent(float size, TextComponent component);
    TextComponent ItalicTextComponent(TextComponent component);
    TextComponent BoldTextComponent(TextComponent component);

    TextComponent FunctionalTextComponent(Func<string> supplier);
    TextComponent FunctionalTextComponent(Func<string> supplier, string textForCompare);

    GUIWidget Masked(Virial.Media.GUIWidget inner);
    GUIWidget ButtonGrouped(Virial.Media.GUIWidget inner);

    internal void OpenAssignableFilterWindow<R>(string scrollerTag, IEnumerable<R> allRoles, Func<R, bool> test, Action<R> toggleAndShare) where R : DefinedAssignable;

    /// <summary>
    /// オーバーレイを表示します。
    /// </summary>
    /// <param name="widget"></param>
    /// <param name="clickable"></param>
    void ShowOverlay(GUIWidget widget, GUIClickable? clickable = null);
    /// <summary>
    /// オーバーレイを隠します。
    /// </summary>
    void HideOverlay();
    /// <summary>
    /// 指定のGUIClickableと紐づけられたオーバーレイが表示中の場合、隠します。
    /// </summary>
    /// <param name="clickable"></param>
    void HideOverlayIf(GUIClickable? clickable);
}

public static class GUIWidgetHelpers
{
    public static GUIWidget Enmask(this GUIWidget inner) => NebulaAPI.GUI.Masked(inner);
    public static GUIWidget AsButtonGroup(this GUIWidget inner) => NebulaAPI.GUI.ButtonGrouped(inner);
    public static GUIWidget Move(this GUIWidget inner, Virial.Compat.Vector2 diff)
    {
        if (diff.x > 0f)
            inner = NebulaAPI.GUI.HorizontalHolder(inner.Alignment, NebulaAPI.GUI.HorizontalMargin(diff.x), inner);
        if (diff.x < 0f)
            inner = NebulaAPI.GUI.HorizontalHolder(inner.Alignment, inner, NebulaAPI.GUI.HorizontalMargin(-diff.x));

        if (diff.y > 0f)
            inner = NebulaAPI.GUI.VerticalHolder(inner.Alignment, NebulaAPI.GUI.VerticalMargin(diff.y), inner);
        if (diff.y < 0f)
            inner = NebulaAPI.GUI.VerticalHolder(inner.Alignment, inner, NebulaAPI.GUI.HorizontalMargin(-diff.y));

        return inner;
    }
    public static GUIWidget WithRoom(this GUIWidget inner, Virial.Compat.Vector2 margin)
    {
        if (margin.x > 0f)
        {
            var xMargin = NebulaAPI.GUI.HorizontalMargin(margin.x * 0.5f);
            inner = NebulaAPI.GUI.HorizontalHolder(inner.Alignment, xMargin, inner, xMargin);
        }
        if (margin.y > 0f)
        {
            var yMargin = NebulaAPI.GUI.VerticalMargin(margin.y * 0.5f);
            inner = NebulaAPI.GUI.VerticalHolder(inner.Alignment, yMargin, inner, yMargin);
        }
        return inner;
    }
}

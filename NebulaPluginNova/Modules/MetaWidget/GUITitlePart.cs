using TMPro;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;

public enum TitlePartType
{
    Prefix = 0,
    Infix = 1,
    Suffix = 2,
}

public class GUITitlePart : NoSGUIText
{
    private const int CellWidth = 128;
    private const int CellHeight = 64;
    private const int SideBorder = 40;
    private const float PixelsPerUnit = 200f;

    private const float BodyLeft = 168f * CellHeight / 440f;
    private const float BodyRight = 765f * CellHeight / 440f;
    private const float TextCenterY = 28f;
    private const float TextPadding = 8f;
    private const float HeightPerTextHeight = 1.2f;
    private const float DefaultCenterScale = 3f;
    private const float DefaultExtraWidth = (CellWidth - SideBorder * 2) * (DefaultCenterScale - 1f);

    static private readonly MultiImage partSprite = DividedExpandableSpriteLoader.FromResource("Nebula.Resources.GUI.TitlePart.png", PixelsPerUnit, SideBorder, 0, 3, 3);
    static internal readonly Image JointSprite = SpriteLoader.FromResource("Nebula.Resources.GUI.TitlePartJoint.png", PixelsPerUnit);
    static public readonly Image GlueIcon = SpriteLoader.FromResource("Nebula.Resources.TitleGlue.png", 100f);

    static public readonly TextAttribute DefaultAttribute = new(GUI.API.GetAttribute(AttributeParams.StandardBaredBoldNonFlexible))
    {
        Color = new(15, 15, 15),
        FontSize = new(1.4f, 0.6f, 1.4f),
        Size = new((BodyRight - BodyLeft + DefaultExtraWidth - TextPadding * 2f) / PixelsPerUnit, CellHeight / HeightPerTextHeight / PixelsPerUnit),
    };

    static public TextAttribute ScaledAttribute(float ratio) => new(DefaultAttribute)
    {
        FontSize = new(DefaultAttribute.FontSize.FontSizeDefault * ratio, DefaultAttribute.FontSize.FontSizeMin * ratio, DefaultAttribute.FontSize.FontSizeMax * ratio),
        Size = new(DefaultAttribute.Size.Width * ratio, DefaultAttribute.Size.Height * ratio),
    };

    public TitlePartType PartType { get; }
    public TitlePartType UsedAs { get; }
    public GUIClickableAction? OnClick { get; init; }
    public bool Selected { get; init; } = false;
    public Virial.Color? BackgroundColor { get; init; }
    public bool IsMasked { get; init; } = false;
    public bool EnlargeOnHover { get; init; } = true;
    internal const float OverlapPerHeight = (CellWidth - (BodyRight - BodyLeft + (168f - 90f) * CellHeight / 440f)) / CellHeight;
    internal static float JointOffsetPerHeight(float pieceWidthPerHeight) => (BodyRight - CellWidth) / CellHeight + pieceWidthPerHeight * 0.5f;
    static private readonly UnityEngine.Color HoverColor = new(1f, 0.93f, 0.6f);
    private const float HoverScale = 1.05f;
    private const float BodyTop = 40f * CellHeight / 440f;

    override protected bool AllowGenerateCollider => OnClick == null;

    public GUITitlePart(GUIAlignment alignment, TextAttribute attribute, TextComponent? text, TitlePartType partType, TitlePartType usedAs) : base(alignment, attribute, text)
    {
        PartType = partType;
        UsedAs = usedAs;
    }

    public GUITitlePart(GUIAlignment alignment, TextComponent? text, TitlePartType partType, TitlePartType usedAs) : this(alignment, DefaultAttribute, text, partType, usedAs) { }

    public GUITitlePart(GUIAlignment alignment, TextAttribute attribute, TextComponent? text, TitlePartType partType) : this(alignment, attribute, text, partType, partType) { }

    public GUITitlePart(GUIAlignment alignment, TextComponent? text, TitlePartType partType) : this(alignment, text, partType, partType) { }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var inner = base.Instantiate(size, out _);
        if (inner == null)
        {
            actualSize = new(0f, 0f);
            return null;
        }

        var innerText = inner.GetComponent<TextMeshPro>();
        if (IsMasked) innerText.fontMaterial = VanillaAsset.StandardMaskedFontMaterial;
        innerText.outlineColor = UnityEngine.Color.clear;
        innerText.color = new(0.14f, 0.14f, 0.14f);
        innerText.fontStyle = FontStyles.Bold;
        var textMaterial = innerText.fontMaterial;
        textMaterial.DisableKeyword("UNDERLAY_ON");
        textMaterial.DisableKeyword("UNDERLAY_INNER");
        if (textMaterial.HasProperty("_UnderlayColor")) textMaterial.SetColor("_UnderlayColor", UnityEngine.Color.clear);
        
        var textFrame = innerText.rectTransform.sizeDelta;
        float scale = textFrame.y * HeightPerTextHeight / (CellHeight / PixelsPerUnit);
        float extraWidth = Mathf.Max(0f, textFrame.x * PixelsPerUnit / scale + TextPadding * 2f - (BodyRight - BodyLeft));
        float pieceWidth = CellWidth + extraWidth;

        var root = UnityHelper.CreateObject("TitlePart", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());

        var background = UnityHelper.CreateObject<SpriteRenderer>("Background", root.transform, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        background.sprite = partSprite.GetSprite((int)UsedAs * 3 + (int)PartType);
        background.drawMode = SpriteDrawMode.Sliced;
        background.tileMode = SpriteTileMode.Continuous;
        background.size = new(pieceWidth / PixelsPerUnit, CellHeight / PixelsPerUnit);
        background.transform.localScale = new UnityEngine.Vector3(scale, scale, 1f);
        if (IsMasked || Attr.Font.FontMaterial != null) background.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var defaultColor = BackgroundColor?.ToUnityColor() ?? UnityEngine.Color.white;
        background.color = defaultColor;

        //スプライトの中心(マスの中心)からテキスト矩形の中心へずらす
        float textX = ((BodyLeft + BodyRight + extraWidth) * 0.5f - pieceWidth * 0.5f) / PixelsPerUnit * scale;
        float textY = (CellHeight * 0.5f - TextCenterY) / PixelsPerUnit * scale;
        inner.transform.SetParent(root.transform);
        inner.transform.localPosition = new UnityEngine.Vector3(textX, textY, -0.05f);

        if (Selected)
        {
            //テキスト矩形の右上の角に重ねる
            float checkX = ((BodyRight + extraWidth) - pieceWidth * 0.5f) / PixelsPerUnit * scale;
            float checkY = (CellHeight * 0.5f - BodyTop) / PixelsPerUnit * scale;
            var checkBack = UnityHelper.CreateObject<SpriteRenderer>("Checkmark", root.transform, new UnityEngine.Vector3(checkX - 0.03f, checkY - 0.11f, -0.1f), LayerExpansion.GetUILayer());
            checkBack.sprite = GUIModernButton.ModernCheckBackSprite;
            checkBack.maskInteraction = background.maskInteraction;
            var checkFront = UnityHelper.CreateObject<SpriteRenderer>("Checkmark", checkBack.transform, new UnityEngine.Vector3(0.02f, 0.01f, -0.05f), LayerExpansion.GetUILayer());
            checkFront.sprite = GUIModernButton.ModernCheckSprite;
            checkFront.maskInteraction = background.maskInteraction;
        }

        if (OnClick != null)
        {
            //凸・凹を除いたテキスト矩形の範囲をクリック範囲とする
            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.offset = new(textX, 0f);
            collider.size = new((BodyRight - BodyLeft + extraWidth) / PixelsPerUnit * scale, (CellHeight - BodyTop * 2f) / PixelsPerUnit * scale);

            var button = root.SetUpButton(true);
            var clickable = new GUIClickable(button);
            button.OnMouseOver.AddListener(() =>
            {
                background.color = defaultColor * HoverColor;
                if (EnlargeOnHover) root.transform.localScale = new UnityEngine.Vector3(HoverScale, HoverScale, 1f);
            });
            button.OnMouseOut.AddListener(() =>
            {
                background.color = defaultColor;
                root.transform.localScale = UnityEngine.Vector3.one;
            });
            button.OnClick.AddListener(() => OnClick(clickable));

            if (OverlayWidget != null)
            {
#if PC
                button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, OverlayWidget()));
                button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
#endif
            }
        }

        actualSize = new(background.size.x * scale, background.size.y * scale);
        return root;
    }
}

public record TitlePartEntry(TextComponent? Text, TitlePartType PartType)
{
    public GUIClickableAction? OnClick { get; init; }
    public bool Selected { get; init; } = false;
    public Virial.Color? BackgroundColor { get; init; }
    public GUIWidgetSupplier? OverlayWidget { get; init; }
}

public class GUITitlePartSequence : AbstractGUIWidget
{
    private readonly TextAttribute attribute;
    private readonly IReadOnlyList<TitlePartEntry> entries;
    public bool IsMasked { get; init; } = false;

    public GUITitlePartSequence(GUIAlignment alignment, TextAttribute attribute, IEnumerable<TitlePartEntry> entries) : base(alignment)
    {
        this.attribute = attribute;
        this.entries = entries.ToArray();
    }

    public GUITitlePartSequence(GUIAlignment alignment, IEnumerable<TitlePartEntry> entries) : this(alignment, GUITitlePart.DefaultAttribute, entries) { }

    static public TitlePartType GetUsage(int index, int count, TitlePartType ownType)
    {
        if (count <= 1) return ownType;
        if (index == 0) return TitlePartType.Prefix;
        if (index == count - 1) return TitlePartType.Suffix;
        return TitlePartType.Infix;
    }

    private const float DepthPerPiece = 0.02f;

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        actualSize = new(0f, 0f);
        if (entries.Count == 0) return null;

        var root = UnityHelper.CreateObject("TitlePartSequence", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());

        List<(GameObject obj, Size size)> pieces = [];
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var widget = new GUITitlePart(GUIAlignment.Center, attribute, entry.Text, entry.PartType, GetUsage(i, entries.Count, entry.PartType))
            {
                OnClick = entry.OnClick,
                Selected = entry.Selected,
                BackgroundColor = entry.BackgroundColor,
                OverlayWidget = entry.OverlayWidget,
                IsMasked = IsMasked,
                EnlargeOnHover = false,
            };
            var obj = widget.Instantiate(new(100f, 100f), out var pieceSize);
            if (obj == null) continue;
            obj.transform.SetParent(root.transform);
            pieces.Add((obj, pieceSize));
        }
        if (pieces.Count == 0)
        {
            GameObject.Destroy(root);
            return null;
        }

        //同じ属性から作られるため、すべてのピースは同じ大きさになる
        float pieceWidth = pieces[0].size.Width;
        float pieceHeight = pieces[0].size.Height;
        float step = pieceWidth - pieceHeight * GUITitlePart.OverlapPerHeight;
        float totalWidth = pieceWidth + step * (pieces.Count - 1);

        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].obj.transform.localPosition = new UnityEngine.Vector3(-totalWidth * 0.5f + pieceWidth * 0.5f + step * i, 0f, -DepthPerPiece * i);
        }

        float jointX = GUITitlePart.JointOffsetPerHeight(pieceWidth / pieceHeight) * pieceHeight;
        float jointScale = pieceHeight / (GUITitlePart.JointSprite.GetSprite().bounds.size.y);
        for (int i = 0; i + 1 < entries.Count && i + 1 < pieces.Count; i++)
        {
            if (entries[i].PartType != TitlePartType.Suffix || entries[i + 1].PartType != TitlePartType.Prefix) continue;
            var joint = UnityHelper.CreateObject<SpriteRenderer>("Joint", root.transform,
                new UnityEngine.Vector3(-totalWidth * 0.5f + pieceWidth * 0.5f + step * i + jointX, 0f, -DepthPerPiece * (i + 1) - 0.01f), LayerExpansion.GetUILayer());
            joint.sprite = GUITitlePart.JointSprite.GetSprite();
            joint.transform.localScale = new UnityEngine.Vector3(jointScale, jointScale, 1f);
            if (IsMasked || attribute.Font.FontMaterial != null) joint.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        actualSize = new(totalWidth, pieceHeight);
        return root;
    }
}

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
    static private readonly IDividedSpriteLoader sealingSprite = DividedSpriteLoader.FromResource("Nebula.Resources.GUI.TitleCharacterSealing.png", 100f, 50, 50, true);
    private const float NarrowCharacterRatio = 0.6f;
    private const float ThinCharacterRatio = 0.4f;
    private const float LowCharacterTopPerEm = 0.4f;
    private const float SealingHeightPerEm = 1.6f;
    private const float SealingCenterPerEm = 0.32f;
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
    /// <summary>指定すると、毎フレームこの値でチェックマークの表示を切り替える。作り直さずに選択状態を反映したいときに使う。</summary>
    public Func<bool>? SelectedGetter { get; init; } = null;
    public Virial.Color? BackgroundColor { get; init; }
    public bool IsMasked { get; init; } = false;
    /// <summary>この属性で作ったパーツの高さ</summary>
    static public float GetPieceHeight(TextAttribute attribute) => attribute.Size.Height * HeightPerTextHeight;
    public bool EnlargeOnHover { get; init; } = true;
    public Action<int>? OnClickCharacter { get; init; }
    public IReadOnlyCollection<int>? SealedCharacters { get; init; }
    internal const float OverlapPerHeight = (CellWidth - (BodyRight - BodyLeft + (168f - 90f) * CellHeight / 440f)) / CellHeight;
    internal static float JointOffsetPerHeight(float pieceWidthPerHeight) => (BodyRight - CellWidth) / CellHeight + pieceWidthPerHeight * 0.5f;
    /// <summary>ピースの中心から見た、テキスト矩形(凸・凹を除いた本体)の中心のx座標。ピースの高さに対する比。</summary>
    internal const float BodyCenterOffsetPerHeight = (BodyLeft + BodyRight - CellWidth) * 0.5f / CellHeight;
    static private readonly UnityEngine.Color HoverColor = new(1f, 0.93f, 0.6f);
    private const float HoverScale = 1.05f;
    private const float BodyTop = 40f * CellHeight / 440f;

    override protected bool AllowGenerateCollider => OnClick == null;

    /// <summary>元のマテリアルのインスタンスID → 下線(Underlay)と縁取りを消したマテリアル</summary>
    static private readonly Dictionary<int, Material> textMaterialCache = new();
    static private Material GetTextMaterial(Material baseMaterial)
    {
        int id = baseMaterial.GetInstanceID();
        if (textMaterialCache.TryGetValue(id, out var cached) && cached.AsBoolFast()) return cached;

        var material = new Material(baseMaterial);
        material.DisableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        if (material.HasProperty("_UnderlayColor")) material.SetColor("_UnderlayColor", UnityEngine.Color.clear);
        if (material.HasProperty("_OutlineColor")) material.SetColor("_OutlineColor", UnityEngine.Color.clear);
        textMaterialCache[id] = material;
        return material;
    }

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
        //fontMaterialを読むとパーツごとにマテリアルが複製されるため、元のマテリアルごとに加工済みのものを使いまわす
        innerText.fontSharedMaterial = GetTextMaterial(innerText.fontSharedMaterial);
        innerText.outlineColor = UnityEngine.Color.clear;
        innerText.color = new(0.14f, 0.14f, 0.14f);
        innerText.fontStyle = FontStyles.Bold;

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

        if (SealedCharacters != null && SealedCharacters.Count > 0) AddSealings(innerText, background.maskInteraction);

        if (Selected || SelectedGetter != null)
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

            if (SelectedGetter != null)
            {
                var getter = SelectedGetter;
                var checkObj = checkBack.gameObject;
                checkObj.SetActive(getter());
                root.AddComponent<Nebula.Behavior.ScriptBehaviour>().UpdateHandler += () =>
                {
                    bool selected = getter();
                    if (checkObj.activeSelf != selected) checkObj.SetActive(selected);
                };
            }
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
            button.OnClick.AddListener(() =>
            {
                if (OnClickCharacter != null)
                {
                    var charIndex = FindCharacterAtMouse(innerText);
                    if (charIndex >= 0)
                    {
                        OnClickCharacter(charIndex);
                        return;
                    }
                }
                OnClick(clickable);
            });

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

    /// <summary>塗りつぶした文字に重ねる画像の種類</summary>
    private const int SealingNormal = 0;
    private const int SealingLowThin = 3;

    //連続して塗りつぶした文字に重ねる画像。(文字の種類, 文字数) → 画像中の範囲(px、左下原点)
    static private readonly ResourceTextureLoader additionalSealingTexture = new("Nebula.Resources.GUI.TitleCharacterSealingAdditional.png");
    static private readonly Dictionary<(int kind, int count), Rect> additionalSealingRects = new()
    {
        { (SealingNormal, 2), new(0f, 100f, 100f, 50f) },
        { (SealingLowThin, 3), new(100f, 100f, 50f, 50f) },
        { (SealingNormal, 3), new(0f, 50f, 150f, 50f) },
        { (SealingLowThin, 2), new(150f, 50f, 50f, 50f) },
        { (SealingNormal, 4), new(0f, 0f, 150f, 50f) },
    };
    static private readonly Dictionary<(int kind, int count), Sprite> additionalSealingSprites = new();
    static private Sprite? GetSealingSprite(int kind, int count)
    {
        if (count == 1) return sealingSprite.GetSprite(kind);
        if (additionalSealingSprites.TryGetValue((kind, count), out var sprite) && sprite.AsBoolFast()) return sprite;
        if (!additionalSealingRects.TryGetValue((kind, count), out var rect)) return null;
        sprite = additionalSealingTexture.GetTexture().ToSprite(rect, new(0.5f, 0.5f), 100f, SpriteMeshType.Tight);
        additionalSealingSprites[(kind, count)] = sprite;
        return sprite;
    }

    /// <summary>連続した文字数を、まとめて覆う画像の文字数(1～3)に分ける</summary>
    static private IEnumerable<int> SplitSealingRun(int count, int kind)
    {
        //1文字だけ余らないように分ける(全角は4文字、ピリオド類は3文字まで)
        while (count > 0)
        {
            int size = kind == SealingNormal ?
                (count <= 4 ? count : count == 5 ? 3 : 4) :
                (count <= 3 ? count : count == 4 ? 2 : 3);
            yield return size;
            count -= size;
        }
    }

    private int GetSealingKind(TMP_CharacterInfo info, float em)
    {
        float advance = info.xAdvance - info.origin;
        if (advance >= em * NarrowCharacterRatio) return SealingNormal;
        if (advance >= em * ThinCharacterRatio) return 1;
        if (info.isVisible && info.topRight.y < info.baseLine + em * LowCharacterTopPerEm) return SealingLowThin;
        return 2;
    }

    /// <summary>塗りつぶした文字の上に、のりの画像を重ねる。同じ種類の文字が連続して塗りつぶされている場合は、まとめて覆う画像を使う。</summary>
    private void AddSealings(TextMeshPro text, SpriteMaskInteraction maskInteraction)
    {
        text.ForceMeshUpdate();
        var textInfo = text.textInfo;

        //塗りつぶした文字を、位置が連続していて種類が同じものごとにまとめる
        List<List<(TMP_CharacterInfo info, int kind)>> runs = [];
        int lastIndex = -2;
        foreach (var index in SealedCharacters!.Where(i => i >= 0 && i < textInfo.characterCount).Distinct().OrderBy(i => i))
        {
            var info = textInfo.characterInfo[index];
            //TextMeshProでは、ローカル座標での1emはフォントサイズの0.1倍
            float em = info.pointSize * 0.1f;
            if (em <= 0f) continue;
            int kind = GetSealingKind(info, em);
            bool canJoin = kind is SealingNormal or SealingLowThin;
            if (canJoin && index == lastIndex + 1 && runs.Count > 0 && runs[^1][^1].kind == kind)
                runs[^1].Add((info, kind));
            else
                runs.Add([(info, kind)]);
            lastIndex = index;
        }

        foreach (var run in runs)
        {
            int offset = 0;
            foreach (var count in SplitSealingRun(run.Count, run[0].kind))
            {
                var first = run[offset].info;
                var last = run[offset + count - 1].info;
                offset += count;

                var sprite = GetSealingSprite(run[0].kind, count);
                if (sprite == null) continue;
                float em = first.pointSize * 0.1f;

                var renderer = UnityHelper.CreateObject<SpriteRenderer>("Sealing", text.transform,
                    new UnityEngine.Vector3((first.origin + last.xAdvance) * 0.5f, first.baseLine + em * SealingCenterPerEm, -0.01f), LayerExpansion.GetUILayer());
                renderer.sprite = sprite;
                renderer.maskInteraction = maskInteraction;
                //1文字用の画像と同じ縮尺にする(どの画像も1行の高さは50px)
                float scale = em * SealingHeightPerEm / sprite.bounds.size.y;
                renderer.transform.localScale = new UnityEngine.Vector3(scale, scale, 1f);
            }
        }
    }

    /// <summary>マウスの下にある文字の位置を返す。無ければ-1。</summary>
    static private int FindCharacterAtMouse(TextMeshPro text)
    {
        var local = text.transform.InverseTransformPoint(UnityHelper.ScreenToWorldPoint(Input.mousePosition, text.gameObject.layer));
        var textInfo = text.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var info = textInfo.characterInfo[i];
            //空白なども含め、送り幅の範囲で判定する
            float left = info.origin;
            float right = info.xAdvance;
            if (info.isVisible)
            {
                left = Mathf.Min(left, info.bottomLeft.x);
                right = Mathf.Max(right, info.topRight.x);
            }
            if (left <= local.x && local.x < right) return i;
        }
        return -1;
    }
}

public record TitlePartEntry(TextComponent? Text, TitlePartType PartType)
{
    public GUIClickableAction? OnClick { get; init; }
    public bool Selected { get; init; } = false;
    public Virial.Color? BackgroundColor { get; init; }
    public GUIWidgetSupplier? OverlayWidget { get; init; }
    public Action<int>? OnClickCharacter { get; init; }
    public IReadOnlyCollection<int>? SealedCharacters { get; init; }
    /// <summary>このパーツに使っているのりの数。GlueBandHeightが正のとき、0より大きければパーツの真上に表示される。</summary>
    public int GlueCount { get; init; } = 0;
}

public class GUITitlePartSequence : AbstractGUIWidget
{
    private readonly TextAttribute attribute;
    private readonly IReadOnlyList<TitlePartEntry> entries;
    public bool IsMasked { get; init; } = false;
    /// <summary>
    /// パーツの上に確保する、のりの数を表示する帯の高さ。0以下なら帯を確保しない。
    /// のりを使っているパーツが無くても帯の高さは確保されるため、表示位置はずれない。
    /// </summary>
    public float GlueBandHeight { get; init; } = 0f;

    static private readonly TextAttribute GlueCountAttribute = new(GUI.API.GetAttribute(AttributeParams.StandardBaredBoldLeftNonFlexible)) { FontSize = new(1.3f), Size = new(0.4f, 0.2f) };

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

        List<(GameObject obj, Size size, TitlePartEntry entry)> pieces = [];
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var widget = new GUITitlePart(GUIAlignment.Center, attribute, entry.Text, entry.PartType, GetUsage(i, entries.Count, entry.PartType))
            {
                OnClick = entry.OnClick,
                Selected = entry.Selected,
                BackgroundColor = entry.BackgroundColor,
                OverlayWidget = entry.OverlayWidget,
                OnClickCharacter = entry.OnClickCharacter,
                SealedCharacters = entry.SealedCharacters,
                IsMasked = IsMasked,
                EnlargeOnHover = false,
            };
            var obj = widget.Instantiate(new(100f, 100f), out var pieceSize);
            if (obj == null) continue;
            obj.transform.SetParent(root.transform);
            pieces.Add((obj, pieceSize, entry));
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

        //帯を上に確保する分、ピースを下にずらして全体の中心を原点に合わせる
        float bandHeight = Mathf.Max(0f, GlueBandHeight);
        float piecesY = -bandHeight * 0.5f;

        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].obj.transform.localPosition = new UnityEngine.Vector3(-totalWidth * 0.5f + pieceWidth * 0.5f + step * i, piecesY, -DepthPerPiece * i);
        }

        if (bandHeight > 0f) AddGlueCounts(root, pieces, pieceWidth, pieceHeight, step, totalWidth, bandHeight);

        float jointX = GUITitlePart.JointOffsetPerHeight(pieceWidth / pieceHeight) * pieceHeight;
        float jointScale = pieceHeight / (GUITitlePart.JointSprite.GetSprite().bounds.size.y);
        for (int i = 0; i + 1 < entries.Count && i + 1 < pieces.Count; i++)
        {
            if (entries[i].PartType != TitlePartType.Suffix || entries[i + 1].PartType != TitlePartType.Prefix) continue;
            var joint = UnityHelper.CreateObject<SpriteRenderer>("Joint", root.transform,
                new UnityEngine.Vector3(-totalWidth * 0.5f + pieceWidth * 0.5f + step * i + jointX, piecesY, -DepthPerPiece * (i + 1) - 0.01f), LayerExpansion.GetUILayer());
            joint.sprite = GUITitlePart.JointSprite.GetSprite();
            joint.transform.localScale = new UnityEngine.Vector3(jointScale, jointScale, 1f);
            if (IsMasked || attribute.Font.FontMaterial != null) joint.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        actualSize = new(totalWidth, pieceHeight + bandHeight);
        return root;
    }

    /// <summary>のりを使っているパーツの本体の真上に、のりのアイコンと、その右上に使用数を置く。</summary>
    private void AddGlueCounts(GameObject root, List<(GameObject obj, Size size, TitlePartEntry entry)> pieces, float pieceWidth, float pieceHeight, float step, float totalWidth, float bandHeight)
    {
        //帯の中心は、ピースの上端から帯の高さの半分だけ上
        float bandCenterY = pieceHeight * 0.5f;
        float iconSize = bandHeight * 0.85f;
        var iconSprite = GUITitlePart.GlueIcon.GetSprite();
        float iconScale = iconSize / Mathf.Max(iconSprite.bounds.size.x, iconSprite.bounds.size.y);
        bool masked = IsMasked || attribute.Font.FontMaterial != null;

        for (int myPiece = 0; myPiece < pieces.Count; myPiece++)
        {
            int count = pieces[myPiece].entry.GlueCount;
            if (count <= 0) continue;

            float x = -totalWidth * 0.5f + pieceWidth * 0.5f + step * myPiece + GUITitlePart.BodyCenterOffsetPerHeight * pieceHeight;
            var icon = UnityHelper.CreateObject<SpriteRenderer>("GlueIcon", root.transform, new UnityEngine.Vector3(x, bandCenterY, -0.1f), LayerExpansion.GetUILayer());
            icon.sprite = iconSprite;
            icon.transform.localScale = new UnityEngine.Vector3(iconScale, iconScale, 1f);
            if (masked) icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var countText = new NoSGUIText(GUIAlignment.Left, GlueCountAttribute, GUI.API.RawTextComponent(count.ToString())).Instantiate(new(100f, 100f), out _);
            if (countText == null) continue;
            countText.transform.SetParent(root.transform);
            //テキスト矩形は中心が原点なので、左端がアイコンの右端に来るようにずらす
            countText.transform.localPosition = new UnityEngine.Vector3(x + iconSize * 0.4f + GlueCountAttribute.Size.Width * 0.5f, bandCenterY + iconSize * 0.3f, -0.15f);
        }
    }
}

using Nebula.Behavior;
using TMPro;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;

public class GUIButton : NoSGUIText
{
    static private MultiImage modernButtonSprite = DividedExpandableSpriteLoader.FromResource("Nebula.Resources.GUI.Button.png", 150f, 12, 12, 3, 2);
    static private MultiImage modernCheckSprite = DividedSpriteLoader.FromResource("Nebula.Resources.GUI.Checkmark.png", 150f, 2, 1);
    public GUIClickableAction? OnClick { get; init; }
    public GUIClickableAction? OnRightClick { get; init; }
    public GUIClickableAction? OnMouseOver { get; init; }
    public GUIClickableAction? OnMouseOut { get; init; }
    public Color? Color { get; init; }
    public Color? SelectedColor { get; init; }

    public string RawText { init { Text = new RawTextComponent(value); } }
    public string TranslationKey { init { Text = new TranslateTextComponent(value); } }
    public bool AsMaskedButton { get; init; }
    public float? TextMargin { get; init; } = null;
    private float GetTextMargin() => TextMargin ?? (IsModern ? 0.05f : 0.26f);
    override protected bool AllowGenerateCollider => false;
    public bool IsModern = false;


    public GUIButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text, bool modern = false) : base(alignment,attribute,text)
    {
        IsModern = modern;
        Attr = attribute;
        AsMaskedButton = attribute.Font.FontMaterial != null;
    }

    static private Virial.Color piledModernColor = new(41 * 0.5f, 235 * 0.5f, 198 * 0.5f);
    static private Virial.Color selectedModernColor = new(16, 16, 16);

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var inner = base.Instantiate(size, out actualSize)!;

        var margin = GetTextMargin();

        var button = UnityHelper.CreateObject<SpriteRenderer>("Button", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        button.sprite = IsModern ? modernButtonSprite.GetSprite(0) : VanillaAsset.TextButtonSprite;
        button.drawMode = SpriteDrawMode.Sliced;
        button.tileMode = SpriteTileMode.Continuous;
        button.size = actualSize.ToUnityVector() + new UnityEngine.Vector2(margin * 0.84f, margin * 0.84f);
        if (AsMaskedButton) button.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        inner.transform.SetParent(button.transform);
        inner.transform.localPosition += new UnityEngine.Vector3(0, 0, -0.05f);

        var collider = button.gameObject.AddComponent<BoxCollider2D>();
        collider.size = actualSize.ToUnityVector() + new UnityEngine.Vector2(margin * 0.6f, margin * 0.6f);
        collider.isTrigger = true;

        var passiveButton = button.gameObject.SetUpButton(true, IsModern ? null : button, Color, SelectedColor);
        GUIClickable clickable = new(passiveButton);

        if (IsModern)
        {
            var innerText = inner.GetComponent<TextMeshPro>();
            innerText.outlineColor = UnityEngine.Color.clear;

            var piledText = Text?.Color(piledModernColor);
            var selectedText = Text?.Color(selectedModernColor);

            passiveButton.OnMouseOver.AddListener(() =>
            {
                button.sprite = modernButtonSprite.GetSprite(1);
                if (Text != null) innerText.text = piledText!.GetString();
            });
            passiveButton.OnMouseOut.AddListener(() =>
            {
                button.sprite = modernButtonSprite.GetSprite(0);
                if (Text != null) innerText.text = Text!.GetString();
            });
        }

        if (OnClick != null) passiveButton.OnClick.AddListener(() => OnClick(clickable));
        if (OnMouseOut != null) passiveButton.OnMouseOut.AddListener(() => OnMouseOut(clickable));
        if (OnMouseOver != null) passiveButton.OnMouseOver.AddListener(() => OnMouseOver(clickable));
    

        if(OverlayWidget != null)
        {
            passiveButton.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(passiveButton, OverlayWidget()));
            passiveButton.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(passiveButton));
        }

        if (OnRightClick != null) passiveButton.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked += ()=>OnRightClick(clickable);

        actualSize.Width += margin + 0.1f;
        actualSize.Height += margin + 0.1f;

        return button.gameObject;
    }
}

public class GUISpinButton : AbstractGUIWidget
{
    public Action<bool> OnClick { get; init; }
    public Color? Color { get; init; }
    public Color? SelectedColor { get; init; }

    public bool AsMaskedButton { get; init; } = true;
    public float TextMargin { get; init; } = 0.26f;
    
    /// <summary>
    /// 値の増減ボタンです。
    /// </summary>
    /// <param name="alignment"></param>
    /// <param name="onClick">値が大きくなるときtrueが渡されるコールバック</param>
    public GUISpinButton(GUIAlignment alignment, Action<bool> onClick) : base(alignment)
    {
        OnClick = onClick;
    }

    static private TextAttribute ButtonTextAttribute = new(GUI.API.GetAttribute(AttributeAsset.OptionsButton)) { Size = new(0.38f, 0.18f) };
    static private TextAttribute ButtonUnmaskedTextAttribute = new(ButtonTextAttribute) { Font = GUI.API.GetFont(FontAsset.Gothic) };
    static private NoSGUIText UpperButtonText = new(GUIAlignment.Center, ButtonTextAttribute, new RawTextComponent("▲"));
    static private NoSGUIText LowerButtonText = new(GUIAlignment.Center, ButtonTextAttribute, new RawTextComponent("▼"));
    static private NoSGUIText UpperUnmaskedButtonText = new(GUIAlignment.Center, ButtonUnmaskedTextAttribute, new RawTextComponent("▲"));
    static private NoSGUIText LowerUnmaskedButtonText = new(GUIAlignment.Center, ButtonUnmaskedTextAttribute, new RawTextComponent("▼"));
    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        GameObject GenerateButton(Transform parent, bool increase)
        {
            var button = UnityHelper.CreateObject<SpriteRenderer>("Button", parent, new(0f, increase ? 0.141f : -0.141f, 0f), LayerExpansion.GetUILayer());
            button.sprite = VanillaAsset.TextButtonSprite;
            button.drawMode = SpriteDrawMode.Sliced;
            button.tileMode = SpriteTileMode.Continuous;
            button.size = new(0.38f + 0.1f, 0.18f + 0.085f);
            if (AsMaskedButton) button.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var textTransform = (increase ? (AsMaskedButton ? UpperButtonText : UpperUnmaskedButtonText) : (AsMaskedButton ? LowerButtonText : LowerUnmaskedButtonText)).Instantiate(new(1f, 1f), out _)!.transform;
            textTransform.SetParent(button.transform);
            textTransform.localPosition = new(0, 0, -0.1f);

            var collider = button.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new(0.38f + 0.1f, 0.18f + 0.05f);
            collider.isTrigger = true;

            var passiveButton = button.gameObject.SetUpButton(true, button, Color, SelectedColor);
            GUIClickable clickable = new(passiveButton);
            if (OnClick != null) passiveButton.OnClick.AddListener(() => OnClick(increase));

            return button.gameObject;
        }

        actualSize = new(0.38f + 0.1f, (0.18f + 0.13f) * 2f);

        GameObject holder = UnityHelper.CreateObject("DualButton", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());

        GenerateButton(holder.transform, true);
        GenerateButton(holder.transform, false);

        return holder;
    }
}

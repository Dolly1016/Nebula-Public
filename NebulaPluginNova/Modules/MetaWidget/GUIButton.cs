using Nebula.Behaviour;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;
public class GUIButton : NoSGUIText
{
    public GUIClickableAction? OnClick { get; init; }
    public GUIClickableAction? OnRightClick { get; init; }
    public GUIClickableAction? OnMouseOver { get; init; }
    public GUIClickableAction? OnMouseOut { get; init; }
    public Color? Color { get; init; }
    public Color? SelectedColor { get; init; }

    public string RawText { init { Text = new RawTextComponent(value); } }
    public string TranslationKey { init { Text = new TranslateTextComponent(value); } }
    public bool AsMaskedButton { get; init; }
    public float TextMargin { get; init; } = 0.26f;
    override protected bool AllowGenerateCollider => false;

    public GUIButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text) : base(alignment,attribute,text)
    {
        Attr = attribute;
        AsMaskedButton = attribute.Font.FontMaterial != null;
    }


    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var inner = base.Instantiate(size, out actualSize)!;

        var button = UnityHelper.CreateObject<SpriteRenderer>("Button", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        button.sprite = VanillaAsset.TextButtonSprite;
        button.drawMode = SpriteDrawMode.Sliced;
        button.tileMode = SpriteTileMode.Continuous;
        button.size = actualSize.ToUnityVector() + new UnityEngine.Vector2(TextMargin * 0.84f, TextMargin * 0.84f);
        if (AsMaskedButton) button.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        inner.transform.SetParent(button.transform);
        inner.transform.localPosition += new UnityEngine.Vector3(0, 0, -0.05f);

        var collider = button.gameObject.AddComponent<BoxCollider2D>();
        collider.size = actualSize.ToUnityVector() + new UnityEngine.Vector2(TextMargin * 0.6f, TextMargin * 0.6f);
        collider.isTrigger = true;

        var passiveButton = button.gameObject.SetUpButton(true, button, Color, SelectedColor);
        GUIClickable clickable = new(passiveButton);
        if (OnClick != null) passiveButton.OnClick.AddListener(()=>OnClick(clickable));
        if (OnMouseOut != null) passiveButton.OnMouseOut.AddListener(()=>OnMouseOut(clickable));
        if (OnMouseOver != null) passiveButton.OnMouseOver.AddListener(()=>OnMouseOver(clickable));

        if(OverlayWidget != null)
        {
            passiveButton.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(passiveButton, OverlayWidget()));
            passiveButton.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(passiveButton));
        }

        if (OnRightClick != null) passiveButton.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked += ()=>OnRightClick(clickable);

        actualSize.Width += TextMargin + 0.1f;
        actualSize.Height += TextMargin + 0.1f;

        return button.gameObject;
    }
}

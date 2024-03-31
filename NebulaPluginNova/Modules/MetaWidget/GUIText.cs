using Nebula.Compat;
using TMPro;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.MetaWidget;


public class NoSGUIText : AbstractGUIWidget
{
    protected Virial.Text.TextAttribute Attr;
    protected TextComponent? Text;
    public GUIWidgetSupplier? OverlayWidget { get; init; } = null;
    public (Action action, bool reopenOverlay)? OnClickText { get; init; } = null;
    virtual protected bool AllowGenerateCollider => true;
    public Action<TextMeshPro>? PostBuilder = null;
    
    public NoSGUIText(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent? text) : base(alignment)
    {
        Attr = attribute;
        Text = text;
    }

    private void ReflectMyAttribute(TMPro.TextMeshPro text, float width)
    {
        text.color = Attr.Color.ToUnityColor();
        text.alignment = (TMPro.TextAlignmentOptions)Attr.Alignment;
        text.fontStyle = (TMPro.FontStyles)Attr.Style;
        text.fontSize = Attr.FontSize.FontSizeDefault;
        text.fontSizeMin = Attr.FontSize.FontSizeMin;
        text.fontSizeMax = Attr.FontSize.FontSizeMax;
        text.enableAutoSizing = Attr.FontSize.AllowAutoSizing;
        text.rectTransform.sizeDelta = new(Math.Min(width, Attr.Size.Width), Attr.Size.Height);
        text.rectTransform.anchorMin = new UnityEngine.Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
        if (Attr.Font != null)
        {
            text.font = Attr.Font.Font;
            if (Attr.Font.FontMaterial != null) text.fontMaterial = Attr.Font.FontMaterial;
        }
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        if(Text == null)
        {
            actualSize = new(0f, 0f);
            return null;
        }

        var text = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, null);
        text.transform.localPosition = new UnityEngine.Vector3(0f, 0f, 0f);

        ReflectMyAttribute(text, size.Width);
        text.text = Text.GetString();
        text.sortingOrder = 10;

        text.ForceMeshUpdate();

        if (Attr.IsFlexible)
        {
            float prefferedWidth = Math.Min(text.rectTransform.sizeDelta.x, text.preferredWidth);
            float prefferedHeight = Math.Min(text.rectTransform.sizeDelta.y, text.preferredHeight);
            text.rectTransform.sizeDelta = new(prefferedWidth, prefferedHeight);

            text.ForceMeshUpdate();
        }

        if (AllowGenerateCollider && (OverlayWidget != null || OnClickText != null))
        {
            var button = text.gameObject.SetUpButton(false);
            var collider = text.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = text.rectTransform.sizeDelta;

            if (OverlayWidget != null)
            {
                button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, OverlayWidget()));
                button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            }
            if (OnClickText != null)
            {
                button.OnClick.AddListener(() =>
                {
                    OnClickText.Value.action.Invoke();
                    if (OnClickText.Value.reopenOverlay)
                    {
                        button.OnMouseOut.Invoke();
                        button.OnMouseOver.Invoke();
                    }
                });
            }
        }

        PostBuilder?.Invoke(text);

        actualSize = new Size(text.rectTransform.sizeDelta);
        return text.gameObject;
    }
}

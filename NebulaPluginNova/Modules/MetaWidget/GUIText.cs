
using TMPro;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;


public class NoSGUIText : AbstractGUIWidget
{
    protected Virial.Text.TextAttribute Attr;
    protected TextComponent? Text;
    public GUIWidgetSupplier? OverlayWidget { get; init; } = null;
    public (Action action, bool reopenOverlay)? OnClickText { get; init; } = null;
    
    /// <summary>
    /// NoSGUITextにオーバーレイ表示やクリック操作を委任する場合はtrueにしてください。
    /// </summary>
    virtual protected bool AllowGenerateCollider => true;
    public Action<TextMeshPro>? PostBuilder = null;
    
    public NoSGUIText(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent? text) : base(alignment)
    {
        Attr = attribute;
        Text = text;
    }

    protected void ReflectMyAttribute(TMPro.TextMeshPro text, float width) => ReflectAttribute(Attr, text, width);
    static public void ReflectAttribute(TextAttribute attr, TMPro.TextMeshPro text, float width)
    {
        text.color = attr.Color.ToUnityColor();
        text.alignment = (TMPro.TextAlignmentOptions)attr.Alignment;
        text.fontStyle = (TMPro.FontStyles)attr.Style;
        text.fontSize = attr.FontSize.FontSizeDefault;
        text.fontSizeMin = attr.FontSize.FontSizeMin;
        text.fontSizeMax = attr.FontSize.FontSizeMax;
        text.enableAutoSizing = attr.FontSize.AllowAutoSizing;
        text.enableWordWrapping = attr.Wrapping;
        text.rectTransform.sizeDelta = new(Math.Min(width, attr.Size.Width), attr.Size.Height);
        text.rectTransform.anchorMin = new UnityEngine.Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
        if (attr.Font != null)
        {
            text.font = attr.Font.Font;
            if (attr.Font.FontMaterial != null) text.fontMaterial = attr.Font.FontMaterial;
        }
        if (attr.OutlineWidth.HasValue) text.SetOutlineThickness(attr.OutlineWidth.Value);
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
            if (text.enableWordWrapping)
            {
                float width = Math.Min(text.rectTransform.sizeDelta.x, text.textBounds.size.x);
                float height = text.textBounds.size.y;
                text.rectTransform.sizeDelta = new(width, height);
            }
            else
            {
                float prefferedWidth = Math.Min(text.rectTransform.sizeDelta.x, text.preferredWidth);
                float prefferedHeight = Math.Min(text.rectTransform.sizeDelta.y, text.preferredHeight);
                text.rectTransform.sizeDelta = new(prefferedWidth, prefferedHeight);
            }

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

public class NoSGUICheckbox : AbstractGUIWidget
{
    internal ListArtifact<(Func<bool> getter, Action toggle)> Artifact = new();
    private bool defaultValue;
    public Action<bool>? OnValueChanged = null;
    public NoSGUICheckbox(GUIAlignment alignment, bool defaultValue) : base(alignment)
    {
        this.defaultValue = defaultValue;
    }


    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        bool currentValue = defaultValue;

        var backText = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, null);
        backText.transform.localPosition = new UnityEngine.Vector3(0f, 0f, 0f);

        var text = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, backText.transform);
        text.transform.localPosition = new UnityEngine.Vector3(0f, 0f, -0.1f);


        NoSGUIText.ReflectAttribute(GUI.API.GetAttribute(AttributeAsset.CenteredBold), backText, size.Width);
        NoSGUIText.ReflectAttribute(GUI.API.GetAttribute(AttributeAsset.CenteredBold), text, size.Width);
        backText.sortingOrder = 10;
        backText.color = Color.white;
        text.sortingOrder = 11;
        text.color = Color.green;

        void UpdateText() => text.text = currentValue ? "✓" : "";

        backText.text = "□";
        text.text = "✓";

        backText.ForceMeshUpdate();
        text.ForceMeshUpdate();

        backText.rectTransform.sizeDelta = new(backText.preferredWidth, backText.preferredHeight);
        text.rectTransform.sizeDelta = new(text.preferredWidth, text.preferredHeight);

        UpdateText();

        backText.ForceMeshUpdate();
        text.ForceMeshUpdate();
        

        var button = text.gameObject.SetUpButton(true);
        var collider = text.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = text.rectTransform.sizeDelta;
        
        button.OnMouseOver.AddListener(() => backText.color = Color.green);
        button.OnMouseOut.AddListener(() => backText.color = Color.white);
        button.OnClick.AddListener(() => { currentValue = !currentValue; UpdateText(); OnValueChanged?.Invoke(currentValue); });

        Artifact.Values.Add((() => currentValue, button.OnClick.Invoke));

        actualSize = new Size(text.rectTransform.sizeDelta);
        return backText.gameObject;
    }
}

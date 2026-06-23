using UnityEngine.Rendering;
using Virial;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;

public class NoSGUIFramed : AbstractGUIWidget
{
    public VVector2 Margin { get; }
    public Virial.Color? Color = null;
    public GUIWidgetSupplier? Inner { get; }
    public Action<SpriteRenderer>? PostBuilder { get; set; }
    public Action? OnClicked { get; init; }

    public NoSGUIFramed(GUIAlignment alignment, GUIWidgetSupplier? inner, VVector2 margin, Virial.Color? color = null) : base(alignment)
    {
        this.Inner = inner;
        this.Margin = margin;
        this.Color = color;
    }

    private void SetAsButton(SpriteRenderer renderer)
    {
        renderer.transform.localPosition = new(0, 0, 0.1f);
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var button = renderer.gameObject.SetUpButton(true, renderer, Color, Virial.Color.Lerp(Virial.Color.Cyan, Virial.Color.Green, 0.4f).AlphaMultiplied(0.3f));
        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
        collider.size = renderer.size;
        collider.isTrigger = true;
        button.OnClick.AddListener(OnClicked);
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var frame = UnityHelper.CreateObject("SizedFrame", null, new(0f, 0f, -0.8f));
        Virial.Media.GUIWidget? innerWidget = Inner?.Invoke();
        actualSize = new(0f, 0f);
        if (innerWidget != null)
        {
            var innerObj = innerWidget.Instantiate(size, out actualSize);
            innerObj?.transform.SetParent(frame.transform, false);
            if (innerObj != null) innerObj.transform.localPosition = new(0f, 0f, -0.1f);
        }

        var renderer = NebulaAsset.CreateSharpBackground(new(actualSize.Width + Margin.x * 1.8f, actualSize.Height + Margin.y * 1.8f), Color ?? Virial.Color.White, frame.transform);
        renderer.gameObject.layer = LayerExpansion.GetUILayer();

        actualSize.Width += Margin.x * 2f;
        actualSize.Height += Margin.y * 2f;

        if (OnClicked != null) SetAsButton(renderer);
        PostBuilder?.Invoke(renderer);

        return frame.gameObject;
    }
}


public class NoSGUIFramedConfiguration : AbstractGUIWidget
{
    private TextComponent textComponent;
    public GUIWidgetSupplier? Inner { get; }
    private Color color;
    static private Virial.Compat.Vector2 margin = new(0.25f, 0.1f);
    static private Virial.Compat.Vector2 outsideMargin = new(0f, 0.15f);
    static private float innerOffsetY = -0.1f;

    static private Image backgroundSprite = SpriteLoader.FromResource("Nebula.Resources.ConfigurationGroup.png", 100f);
    public NoSGUIFramedConfiguration(TextComponent title, GUIWidgetSupplier? inner, Color color) : base(GUIAlignment.Left)
    {
        this.Inner = inner;
        this.textComponent = title;
        this.color = color;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var frame = UnityHelper.CreateObject("SizedFrame", null, new(0f, 0f, -0.8f), LayerExpansion.GetUILayer());
        Virial.Media.GUIWidget? innerWidget = Inner?.Invoke();
        actualSize = new(0f, 0f);
        if (innerWidget != null)
        {
            var innerObj = innerWidget.Instantiate(size, out actualSize);
            innerObj?.transform.SetParent(frame.transform, false);
            if (innerObj != null)
            {
                innerObj.transform.localPosition = new(0f, innerOffsetY, -0.5f);
                innerObj.AddComponent<SortingGroup>().sortingOrder = 2;
            }
        }

        var renderer = NebulaAsset.CreateSharpBackground(new UnityEngine.Vector2(actualSize.Width + margin.x * 1.8f, actualSize.Height + margin.y * 1.8f), new(0.3f, 0.3f, 0.3f, 0.55f), frame.transform);
        renderer.gameObject.layer = LayerExpansion.GetUILayer();
        renderer.transform.localPosition += new UnityEngine.Vector3(0f, innerOffsetY, 0f);
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        renderer.sortingOrder = 0;

        var text = NebulaAPI.GUI.Text(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OptionsGroupTitle), textComponent);
        var left = -renderer.size.x * 0.5f;
        var top = renderer.size.y * 0.5f + innerOffsetY;
        var instantiatedText = text.Instantiate(new Anchor(new(0f,0.5f), new(left, top)), new Size(actualSize.Width - 0.5f, 3f), out var textSize);
        if (instantiatedText.AsBoolFast()) {
            instantiatedText!.transform.SetParent(frame.transform, false);
            instantiatedText.transform.localPosition += new UnityEngine.Vector3(0.1f,0f,-0.5f);

            var textBackRenderer = UnityHelper.CreateObject<SpriteRenderer>("Back", frame.transform, 
                new UnityEngine.Vector3(left + backgroundSprite.GetSprite().bounds.size.x * 0.5f, top ,-0.01f), LayerExpansion.GetUILayer());
            textBackRenderer.sprite = backgroundSprite.GetSprite();
            textBackRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            textBackRenderer.color = color;
            textBackRenderer.sortingOrder = 1;
        }

        

        actualSize.Width += margin.x * 2f + outsideMargin.x * 2f;
        actualSize.Height += margin.y * 2f + outsideMargin.y * 2f + (-innerOffsetY) * 2f;

        return frame.gameObject;
    }
}

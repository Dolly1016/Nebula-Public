using Il2CppInterop.Runtime.Injection;
using Virial.Compat;
using Virial.Media;

namespace Nebula.Modules.GUIWidget;

public class NoSGUIImage : AbstractGUIWidget
{
    protected Virial.Media.Image Image;
    protected FuzzySize Size;
    public Color? Color = null;
    public GUIClickableAction? OnClick;
    public GUIWidgetSupplier? Overlay;
    public bool IsMasked { get; init; }
    public Action<SpriteRenderer>? PostBuilder = null;

    public NoSGUIImage(GUIAlignment alignment, Virial.Media.Image image, FuzzySize size,Color? color = null, GUIClickableAction? onClick = null, GUIWidgetSupplier? overlay = null) : base(alignment)
    {
        this.Image = image;
        this.Size = size;
        this.Color = color;
        this.OnClick = onClick;
        this.Overlay = overlay;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        if(Image?.GetSprite() == null)
        {
            actualSize = new(0f, 0f);
            return null;
        }

        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Image", null, UnityEngine.Vector3.zero,LayerExpansion.GetUILayer());
        renderer.sprite = Image.GetSprite();
        renderer.sortingOrder = 10;
        if (IsMasked) renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        var spriteSize = renderer.sprite.bounds.size;
        float scale = Math.Min(
            Size.Width.HasValue ? (Size.Width.Value / spriteSize.x) : float.MaxValue,
            Size.Height.HasValue ? (Size.Height.Value / spriteSize.y) : float.MaxValue
            );
        renderer.transform.localScale = UnityEngine.Vector3.one * scale;

        if (Color != null) renderer.color = Color.Value;

        actualSize = new(spriteSize.x * scale, spriteSize.y * scale);

        if(OnClick != null || Overlay != null)
        {
            var button = renderer.gameObject.SetUpButton(false, renderer, renderer.color, renderer.color * new UnityEngine.Color(0.7f,1f,0.7f));
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = renderer.sprite.bounds.size;
            collider.isTrigger = true;

            GUIClickable clickable = new(button);
            if (OnClick != null) button.OnClick.AddListener(() => { VanillaAsset.PlaySelectSE(); OnClick.Invoke(clickable); });
            if (Overlay != null)
            {
                button.OnMouseOver.AddListener(() => { VanillaAsset.PlayHoverSE(); NebulaManager.Instance.SetHelpWidget(button, Overlay()); });
                button.OnMouseOut.AddListener(() => { NebulaManager.Instance.HideHelpWidgetIf(button); });
            }
        }

        PostBuilder?.Invoke(renderer);

        return renderer.gameObject;
    }
}

public class GUILoadingIconRotator : MonoBehaviour
{
    static GUILoadingIconRotator() => ClassInjector.RegisterTypeInIl2Cpp<GUILoadingIconRotator>();

    public float Speed = 1f;
    void Update()
    {
        transform.localEulerAngles += new UnityEngine.Vector3(0f, 0f, Speed * 480f * Time.deltaTime);
    }
}
public class GUILoadingIcon : AbstractGUIWidget
{
    static private Image LoadingSprite = SpriteLoader.FromResource("Nebula.Resources.LoadingIcon.png", 100f);
    public float Speed { get; init; } = 1f;
    public float Size { get; init; } = 1f;
    public bool IsMasked { get; init; } = false;

    public GUILoadingIcon(GUIAlignment alignment) : base(alignment)
    {
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Image", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        renderer.gameObject.AddComponent<GUILoadingIconRotator>();
        renderer.sprite = LoadingSprite.GetSprite();
        renderer.sortingOrder = 10;
        if (IsMasked) renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        renderer.transform.localScale = UnityEngine.Vector3.one * Size;

        actualSize = new(renderer.size * Size);

        return renderer.gameObject;
    }
}
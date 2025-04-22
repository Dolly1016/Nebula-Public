
using Nebula.Modules.MetaWidget;
using Nebula.Roles;
using Virial.Assignable;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;

public abstract class AbstractGUIWidget : Virial.Media.GUIWidget
{
    internal override GUIAlignment Alignment => alignment;
    private GUIAlignment alignment;
    internal override GameObject? Instantiate(Anchor anchor, Size size, out Size actualSize)
    {
        var obj = Instantiate(size, out actualSize);

        if (obj != null)
        {
            UnityEngine.Vector3 localPos = anchor.anchoredPosition.ToUnityVector() -
                new UnityEngine.Vector3(
                    actualSize.Width * (anchor.pivot.x - 0.5f),
                    actualSize.Height * (anchor.pivot.y - 0.5f),
                    0f);

            obj.transform.localPosition = localPos;
        }

        return obj;
    }

    public AbstractGUIWidget(GUIAlignment alignment)
    {
        this.alignment = alignment;
    }

    protected static float CalcWidth(GUIAlignment alignment, float myWidth, float maxWidth)
    {
        return Calc(alignment, myWidth, maxWidth, GUIAlignment.Left, GUIAlignment.Right);
    }

    protected static float CalcHeight(GUIAlignment alignment, float myHeight, float maxHeight)
    {
        return Calc(alignment, myHeight, maxHeight, GUIAlignment.Bottom, GUIAlignment.Top);
    }

    private static float Calc(GUIAlignment alignment, float myParam, float maxParam, GUIAlignment lower, GUIAlignment higher)
    {
        if ((int)(alignment & lower) != 0)
            return (myParam - maxParam) * 0.5f;
        if ((int)(alignment & higher) != 0)
            return (maxParam - myParam) * 0.5f;
        return 0f;
    }
}

public abstract class WidgetsHolder : AbstractGUIWidget
{
    protected IEnumerable<Virial.Media.GUIWidget> widgets;

    public WidgetsHolder(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> widgets) : base(alignment)
    {
        this.widgets = widgets.Where(w => w != null)!;
    }
}

public class VerticalWidgetsHolder : WidgetsHolder {

    public VerticalWidgetsHolder(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> widgets) : base(alignment, widgets) { }
    public VerticalWidgetsHolder(GUIAlignment alignment, params Virial.Media.GUIWidget?[] widgets) : base(alignment, widgets) { }
    public float? FixedWidth { get; init; } = null;
    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var results = widgets.Select(c => (c.Instantiate(size, out var acSize), acSize, c)).ToArray();
        (float maxWidth, float sumHeight) = results.Aggregate((0f, 0f), (r, current) => (Math.Max(r.Item1, current.acSize.Width), r.Item2 + current.acSize.Height));
        if (FixedWidth != null) maxWidth = FixedWidth.Value;

        GameObject myObj = UnityHelper.CreateObject("WidgetsHolder", null, UnityEngine.Vector3.zero);


        float height = sumHeight * 0.5f;
        foreach (var r in results)
        {
            if (r.Item1 != null)
            {
                r.Item1.transform.SetParent(myObj.transform);
                r.Item1.transform.localPosition = new UnityEngine.Vector3(CalcWidth(r.c.Alignment, r.acSize.Width, maxWidth), height - r.acSize.Height * 0.5f, 0f);
            }
            height -= r.acSize.Height;
        }

        actualSize = new(maxWidth, sumHeight);
        return myObj;
    }
}

public class HorizontalWidgetsHolder : WidgetsHolder
{

    public HorizontalWidgetsHolder(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> widgets) : base(alignment, widgets) { }
    public HorizontalWidgetsHolder(GUIAlignment alignment, params Virial.Media.GUIWidget?[] widgets) : base(alignment, widgets) { }
    public float? FixedHeight { get; init; } = null;

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var results = widgets.Select(c => (c.Instantiate(size, out var acSize), acSize, c)).ToArray();
        (float sumWidth, float maxHeight) = results.Aggregate((0f, 0f), (r, current) => (r.Item1 + current.acSize.Width, Math.Max(r.Item2, current.acSize.Height)));
        if (FixedHeight != null) maxHeight = FixedHeight.Value;

        GameObject myObj = UnityHelper.CreateObject("WidgetsHolder", null, UnityEngine.Vector3.zero);


        float width = -sumWidth * 0.5f;
        foreach (var r in results)
        {
            if (r.Item1 != null)
            {
                r.Item1.transform.SetParent(myObj.transform);
                r.Item1.transform.localPosition = new UnityEngine.Vector3(width + r.acSize.Width * 0.5f, CalcHeight(r.c.Alignment, r.acSize.Height, maxHeight), 0f);
            }
            width += r.acSize.Width;
        }

        actualSize = new(sumWidth, maxHeight);
        return myObj;
    }
}

public class GUIEmptyWidget : AbstractGUIWidget
{
    static public GUIEmptyWidget Default = new();

    public GUIEmptyWidget(GUIAlignment alignment = GUIAlignment.Left) : base(alignment)
    {
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        actualSize = new(0f,0f);
        return null;
    }
}


public class NoSGUIMargin : AbstractGUIWidget
{
    protected UnityEngine.Vector2 margin;

    public NoSGUIMargin(GUIAlignment alignment, UnityEngine.Vector2 margin) : base(alignment)
    {
        this.margin = margin;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        actualSize = new(margin);
        return null;
    }
}

public class NoSGameObjectGUIWrapper : AbstractGUIWidget
{
    private Func<(GameObject gameObject, Size size)> generator;

    public NoSGameObjectGUIWrapper(GUIAlignment alignment, Func<(GameObject gameObject, Size size)> generator) : base(alignment)
    {
        this.generator = generator;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var generated = generator.Invoke();
        actualSize = generated.size;
        return generated.gameObject;
    }

    public NoSGameObjectGUIWrapper(GUIAlignment alignment, IMetaParallelPlacableOld widget) : base(alignment)
    {
        this.generator = () =>
        {
            var holder = UnityHelper.CreateObject("Holder", null, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
            var height = widget.Generate(holder, UnityEngine.Vector3.zero, out var width);
            return (holder, new(width * 2f, height));
        };
    }
}

public class LazyGUIWidget : AbstractGUIWidget
{
    private GUIWidgetSupplier supprier;
    private Virial.Media.GUIWidget? evaluated = null;
    private bool run = false;

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        if (!run)
        {
            evaluated = supprier.Invoke();
            run = true;
        }
        if(evaluated != null)
            return evaluated.Instantiate(size, out actualSize);
        else
        {
            actualSize = new(0f, 0f);
            return null;
        }
    }

    public LazyGUIWidget(GUIAlignment alignment, GUIWidgetSupplier supplier) : base(alignment)
    {
        this.supprier = supplier;
    }
}

public class NebulaGUIWidgetEngine : Virial.Media.GUI
{
    public static NebulaGUIWidgetEngine Instance { get; private set; } = new();
    public static Virial.Media.GUI API => Instance;

    private Dictionary<AttributeParams, Virial.Text.TextAttribute> allAttr = new();
    private Dictionary<AttributeAsset, Virial.Text.TextAttribute> allAttrAsset = new();
    public Virial.Media.GUIWidget EmptyWidget => GUIEmptyWidget.Default;
    public Virial.Text.TextAttribute GetAttribute(AttributeParams attribute)
    {
        if (allAttr.TryGetValue(attribute, out var attr))
        {
            return attr;
        }
        else
        {
            var isFlexible = attribute.HasFlag((AttributeParams)AttributeTemplateFlag.IsFlexible);
            var newAttr = GenerateAttribute(attribute, new Virial.Color(255, 255, 255), new FontSize(2.2f, 1.2f, 2.5f), new Size(isFlexible ? 10f : 3f, isFlexible ? 10f : 0.5f));
            allAttr[attribute] = newAttr;
            return newAttr;
        }
    }

    public Virial.Text.TextAttribute GetAttribute(AttributeAsset attribute)
    {
        if (!allAttrAsset.TryGetValue(attribute, out var attr))
        {
            allAttrAsset[attribute] = attribute switch
            {
                AttributeAsset.OblongHeader => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.Oblong), Virial.Text.FontStyle.Normal, new(5.2f, false), new(0.45f, 3f), new(255, 255, 255), true),
                AttributeAsset.StandardMediumMasked => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.6f, 0.8f, 1.6f), new(1.45f, 0.3f), new(255, 255, 255), false),
                AttributeAsset.StandardLargeWideMasked => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.7f, 1f, 1.7f), new(2.9f, 0.45f), new(255, 255, 255), false),
                AttributeAsset.CenteredBold => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.9f, 1f, 1.9f), new(8f, 8f), new(255, 255, 255), true),
                AttributeAsset.CenteredBoldFixed => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.9f, 1f, 1.9f), new(1.1f, 0.32f), new(255, 255, 255), false),
                AttributeAsset.LeftBoldFixed => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.9f, 1f, 1.9f), new(1.1f, 0.32f), new(255, 255, 255), false),
                
                AttributeAsset.OverlayTitle => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) },
                AttributeAsset.OverlayContent => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.5f, 1.1f, 1.5f), Size = new(5f, 6f) },
                
                AttributeAsset.DocumentStandard => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.2f, 0.6f, 1.2f), Size = new(7f, 6f) },
                AttributeAsset.DocumentBold => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(1.2f, 0.6f, 1.2f), Size = new(5f, 6f) },
                AttributeAsset.DocumentTitle => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(2.2f, 0.6f, 2.2f), Size = new(5f, 6f) },
                AttributeAsset.DocumentSubtitle1 => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(1.9f, 0.6f, 1.9f), Size = new(5f, 6f) },
                AttributeAsset.DocumentSubtitle2 => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBoldLeft)) { FontSize = new(1.6f, 0.6f, 1.6f), Size = new(5f, 6f) },
                
                AttributeAsset.DeviceButton => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.9f, 1f, 1.9f), new(2.05f, 0.28f), new(255, 255, 255), false),
                
                AttributeAsset.OptionsTitle => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(4f, 0.4f), new(255, 255, 255), false),
                AttributeAsset.OptionsTitleHalf => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(1.8f, 0.4f), new(255, 255, 255), false),
                AttributeAsset.OptionsTitleShortest => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(1f, 0.4f), new(255, 255, 255), false),
                AttributeAsset.OptionsValue => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(1.1f, 0.4f), new(255, 255, 255), false),
                AttributeAsset.OptionsValueShorter => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(0.7f, 0.4f), new(255, 255, 255), false),
                AttributeAsset.OptionsButton => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(0.32f, 0.22f), new(255, 255, 255), false),
                AttributeAsset.OptionsButtonLonger => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(1.8f, 0.22f), new(255, 255, 255), false),
                AttributeAsset.OptionsButtonMedium => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(0.9f, 0.22f), new(255, 255, 255), false),
                AttributeAsset.OptionsFlexible => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(6f, 0.22f), new(255, 255, 255), true),
                AttributeAsset.OptionsGroupTitle => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Normal, new(1.5f, 1f, 1.6f), new(6f, 0.22f), new(255, 255, 255), true, 0f),

                AttributeAsset.MarketplaceTitle => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(2.6f, 1f, 2f), new(3.8f, 0.4f), new(255, 255, 255), false),
                AttributeAsset.MarketplaceDeveloper => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Normal, new(1.4f, 1f, 1.4f), new(2f, 0.32f), new(255, 255, 255), false),
                AttributeAsset.MarketplaceBlurb => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Normal, new(1.4f, 1f, 1.4f), new(6f, 0.3f), new(255, 255, 255), false),
                AttributeAsset.MarketplacePublishButton => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBold)) { FontSize = new(2.2f, 0.6f, 2.2f), Size = new(0.95f, 0.17f) },
                AttributeAsset.MarketplaceCategoryButton => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBoldNonFlexible)) { FontSize = new(1.5f, 0.6f, 1.5f), Size = new(0.95f, 0.32f) },
                AttributeAsset.MarketplaceTabButton => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBoldNonFlexible)) { FontSize = new(1.5f, 0.6f, 1.5f), Size = new(1.3f, 0.35f) },

                AttributeAsset.MeetingTitle => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f,0.6f,1.8f), Size = new(2f,0.5f) },
                AttributeAsset.VersionShower => new TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.Barlow), Virial.Text.FontStyle.Normal, new(1.28f, false), new(1f, 0.4f), new(255, 255, 255), true),
                AttributeAsset.MetaRoleButton => new TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.GothicMasked), Virial.Text.FontStyle.Bold, new(1.8f, 1f, 2f), new(1.4f, 0.26f), new(255, 255, 255), false),
                
                AttributeAsset.SmallWideButton=> new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1f, 0.4f, 1f), Size = new(0.85f, 0.18f) },
                AttributeAsset.SmallArrowButton => new TextAttribute(GUI.Instance.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1f, 0.4f, 1f), Size = new(0.22f, 0.18f) },
                _ => null!
            };
        }
        
        return allAttrAsset[attribute];
    }

    public Virial.Text.TextAttribute GenerateAttribute(AttributeParams attribute, Virial.Color color, FontSize fontSize, Size size)
    {
        Virial.Text.TextAlignment alignment =
            (((AttributeTemplateFlag)(int)attribute) & AttributeTemplateFlag.AlignmentMask) switch
            {
                AttributeTemplateFlag.AlignmentLeft => Virial.Text.TextAlignment.Left,
                AttributeTemplateFlag.AlignmentRight => Virial.Text.TextAlignment.Right,
                _ => Virial.Text.TextAlignment.Center
            };
        Virial.Text.Font font = GetFont(
            (((AttributeTemplateFlag)(int)attribute) & (AttributeTemplateFlag.FontMask | AttributeTemplateFlag.MaterialMask)) switch
            {
                AttributeTemplateFlag.FontStandard | AttributeTemplateFlag.MaterialBared => FontAsset.Gothic,
                AttributeTemplateFlag.FontStandard => FontAsset.GothicMasked,
                AttributeTemplateFlag.FontOblong | AttributeTemplateFlag.MaterialBared => FontAsset.Oblong,
                AttributeTemplateFlag.FontOblong => FontAsset.OblongMasked,
                _ => FontAsset.GothicMasked,
            }
            );

        Virial.Text.FontStyle style = 0;
        if (((AttributeTemplateFlag)attribute & AttributeTemplateFlag.StyleBold) != 0) style |= Virial.Text.FontStyle.Bold;

        bool isFlexible = ((AttributeTemplateFlag)attribute & AttributeTemplateFlag.IsFlexible) != 0;
        return new Virial.Text.TextAttribute(alignment, font, style, fontSize, size, color, isFlexible);
    }

    public Virial.Text.Font GetFont(FontAsset font)
    {
        return font switch
        {
            FontAsset.Prespawn => new StaticFont(null, VanillaAsset.PreSpawnFont),
            FontAsset.Barlow => new StaticFont(null, VanillaAsset.VersionFont),
            FontAsset.Gothic => new DynamicFont(() => null, () => VanillaAsset.StandardTextPrefab.font),
            FontAsset.GothicMasked => new DynamicFont(() => VanillaAsset.StandardMaskedFontMaterial, () => VanillaAsset.StandardTextPrefab.font),
            FontAsset.Oblong => new StaticFont(null, VanillaAsset.BrookFont),
            FontAsset.OblongMasked => new StaticFont(VanillaAsset.OblongMaskedFontMaterial, VanillaAsset.BrookFont),
            _ => new DynamicFont(() => null, () => VanillaAsset.StandardTextPrefab.font),
        };
    }

    public Virial.Media.GUIWidget Arrange(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> inner, int perLine)
    {
        List<Virial.Media.GUIWidget> widgets = new();
        List<Virial.Media.GUIWidget> horizontalWidgets = new();
        foreach(var widget in inner)
        {
            if (widget == null) continue;

            horizontalWidgets.Add(widget);
            if (horizontalWidgets.Count == perLine)
            {
                widgets.Add(HorizontalHolder(alignment, horizontalWidgets.ToArray()));
                horizontalWidgets.Clear();
            }
        }
        if(horizontalWidgets.Count > 0) widgets.Add(HorizontalHolder(alignment, horizontalWidgets));

        return VerticalHolder(alignment, widgets);
    }

    public Virial.Media.GUIWidget HorizontalHolder(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> inner, float? fixedHeight = null) => new HorizontalWidgetsHolder(alignment, inner) { FixedHeight = fixedHeight };

    public Virial.Media.GUIWidget VerticalHolder(GUIAlignment alignment, IEnumerable<Virial.Media.GUIWidget?> inner, float? fixedWidth = null) => new VerticalWidgetsHolder(alignment, inner) { FixedWidth = fixedWidth };

    public Virial.Media.GUIWidget Image(GUIAlignment alignment, Image image, FuzzySize size, GUIClickableAction? onClick = null, GUIWidgetSupplier? overlay = null) => new NoSGUIImage(alignment, image, size, null, onClick, overlay) { IsMasked = true };

    public Virial.Media.GUIWidget ScrollView(GUIAlignment alignment, Size size, string? scrollerTag, Virial.Media.GUIWidget? inner, out Artifact<GUIScreen> artifact) {
        var result = new GUIScrollView(alignment, size.ToUnityVector(), inner) { ScrollerTag = scrollerTag, WithMask = true };
        artifact = result.Artifact;
        return result;
    }

    public Virial.Media.GUIWidget LocalizedButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string translationKey, GUIClickableAction onClick, GUIClickableAction? onMouseOver = null, GUIClickableAction? onMouseOut = null, GUIClickableAction? onRightClick = null, Virial.Color? color = null, Virial.Color? selectedColor = null, float? margin = null)
        => new GUIButton(alignment, attribute, new TranslateTextComponent(translationKey)) { OnClick = onClick, OnMouseOver = onMouseOver, OnMouseOut = onMouseOut, OnRightClick = onRightClick, Color = color?.ToUnityColor(), SelectedColor = selectedColor?.ToUnityColor(), TextMargin = margin ?? 0.26f };
    public Virial.Media.GUIWidget RawButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string rawText, GUIClickableAction onClick, GUIClickableAction? onMouseOver = null, GUIClickableAction? onMouseOut = null, GUIClickableAction? onRightClick = null, Virial.Color? color = null, Virial.Color? selectedColor = null, float? margin = null)
        => new GUIButton(alignment, attribute, new RawTextComponent(rawText)) { OnClick = onClick, OnMouseOver = onMouseOver, OnMouseOut = onMouseOut, OnRightClick = onRightClick, Color = color?.ToUnityColor(), SelectedColor = selectedColor?.ToUnityColor(), TextMargin = margin ?? 0.26f };
    public Virial.Media.GUIWidget Button(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text, GUIClickableAction onClick, GUIClickableAction? onMouseOver = null, GUIClickableAction? onMouseOut = null, GUIClickableAction? onRightClick = null, Virial.Color? color = null, Virial.Color? selectedColor = null, float? margin = null)
        => new GUIButton(alignment, attribute, text) { OnClick = onClick, OnMouseOver = onMouseOver, OnMouseOut = onMouseOut, OnRightClick = onRightClick, Color = color?.ToUnityColor(), SelectedColor = selectedColor?.ToUnityColor(), TextMargin = margin ?? 0.26f };

    public Virial.Media.GUIWidget SpinButton(GUIAlignment alignment, Action<bool> onClick) => new GUISpinButton(alignment, onClick);

    public Virial.Media.GUIWidget LocalizedText(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string translationKey) => new NoSGUIText(alignment, attribute, new TranslateTextComponent(translationKey));
    
    public Virial.Media.GUIWidget RawText(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string rawText) => new NoSGUIText(alignment, attribute, new RawTextComponent(rawText));
    
    public Virial.Media.GUIWidget Text(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text) => new NoSGUIText(alignment, attribute, text);
    

    public Virial.Media.GUIWidget Margin(FuzzySize margin) => new NoSGUIMargin(GUIAlignment.Center, new(margin.Width ?? 0f, margin.Height ?? 0f));

    public TextComponent TextComponent(Virial.Color color, string transrationKey) => TextComponent(color.ToUnityColor(), transrationKey);
    public TextComponent TextComponent(Color color, string transrationKey) => new ColorTextComponent(color, new TranslateTextComponent(transrationKey));
    public TextComponent RawTextComponent(string rawText) => new RawTextComponent(rawText);
    public TextComponent LocalizedTextComponent(string translationKey) => new TranslateTextComponent(translationKey);
    public TextComponent ColorTextComponent(Virial.Color color, TextComponent component) => new ColorTextComponent(color.ToUnityColor(), component);
    public TextComponent SizedTextComponent(float size, TextComponent component) => new SizedTextComponent((int)(size * 100f), component);
    public TextComponent BoldTextComponent(TextComponent component) => new LazyTextComponent(() => component.GetString().Bold());
    public TextComponent ItalicTextComponent(TextComponent component) => new LazyTextComponent(()=> component.GetString().Italic());
    public TextComponent FunctionalTextComponent(Func<string> supplier) => new LazyTextComponent(supplier);
    public TextComponent FunctionalTextComponent(Func<string> supplier, string textForCompare) => new LazyTextComponent(supplier, textForCompare);
    public Virial.Media.GUIWidget Masked(Virial.Media.GUIWidget inner) => new GUIMasking(inner);
    public Virial.Media.GUIWidget ButtonGrouped(Virial.Media.GUIWidget inner) => new GUIButtonGroup(inner);

    public void OpenAssignableFilterWindow<R>(string scrollerTag, IEnumerable<R> allRoles, Func<R, bool> test, Action<R> toggleAndShare) where R : DefinedAssignable
    {
        RoleOptionHelper.OpenFilterScreen<R>(scrollerTag, allRoles, test, null, toggleAndShare);
    }



    public void ShowOverlay(Virial.Media.GUIWidget widget, GUIClickable? clickable = null) => NebulaManager.Instance?.SetHelpWidget(clickable?.uiElement, widget);

    public void HideOverlay() => NebulaManager.Instance.HideHelpWidget();

    public void HideOverlayIf(GUIClickable? clickable) => NebulaManager.Instance.HideHelpWidgetIf(clickable?.uiElement);
}

public class GUIScreenImpl : GUIScreen
{
    GameObject myScreen;
    Anchor myAnchor;
    Size screenSize;

    public GUIScreenImpl(Anchor anchor,Size screenSize, Transform? parent, UnityEngine.Vector3 localPos)
    {
        this.myAnchor = anchor;
        this.screenSize = screenSize;
        this.myScreen = UnityHelper.CreateObject("Screen", parent, localPos, LayerExpansion.GetUILayer());
    }

    public void SetWidget(Virial.Media.GUIWidget? widget, out Size actualSize)
    {
        myScreen.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => GameObject.Destroy(obj)));

        if (widget != null)
        {
            var obj = widget.Instantiate(myAnchor, screenSize, out actualSize);
            if (obj != null) obj.transform.SetParent(myScreen.transform, false);
        }
        else
        {
            actualSize = new(0f, 0f);
        }
    }
}
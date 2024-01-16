using Nebula.Compat;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.MetaContext;

public abstract class AbstractGUIContext : GUIContext
{
    public GUIAlignment Alignment { get; private init; }
    public GameObject? Instantiate(Anchor anchor, Size size, out Size actualSize)
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

    public abstract GameObject? Instantiate(Size size, out Size actualSize);

    public AbstractGUIContext(GUIAlignment alignment)
    {
        Alignment = alignment;
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

public abstract class ContextsHolder : AbstractGUIContext
{
    protected IEnumerable<GUIContext> contexts;

    public ContextsHolder(GUIAlignment alignment, IEnumerable<GUIContext> contexts) : base(alignment)
    {
        this.contexts = contexts;
    }
}

public class VerticalContextsHolder : ContextsHolder {

    public VerticalContextsHolder(GUIAlignment alignment, IEnumerable<GUIContext> contexts) : base(alignment, contexts) { }
    public VerticalContextsHolder(GUIAlignment alignment, params GUIContext[] contexts) : base(alignment, contexts) { }
    public float? FixedWidth { get; init; } = null;
    public override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var results = contexts.Select(c => (c.Instantiate(size, out var acSize), acSize, c)).ToArray();
        (float maxWidth, float sumHeight) = results.Aggregate((0f, 0f), (r, current) => (Math.Max(r.Item1, current.acSize.Width), r.Item2 + current.acSize.Height));
        if (FixedWidth != null) maxWidth = FixedWidth.Value;

        GameObject myObj = UnityHelper.CreateObject("ContextsHolder", null, UnityEngine.Vector3.zero);


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

public class HorizontalContextsHolder : ContextsHolder
{

    public HorizontalContextsHolder(GUIAlignment alignment, IEnumerable<GUIContext> contexts) : base(alignment, contexts) { }
    public HorizontalContextsHolder(GUIAlignment alignment, params GUIContext[] contexts) : base(alignment, contexts) { }
    public float? FixedHeight { get; init; } = null;

    public override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var results = contexts.Select(c => (c.Instantiate(size, out var acSize), acSize, c)).ToArray();
        (float sumWidth, float maxHeight) = results.Aggregate((0f, 0f), (r, current) => (r.Item1 + current.acSize.Width, Math.Max(r.Item2, current.acSize.Height)));
        if (FixedHeight != null) maxHeight = FixedHeight.Value;

        GameObject myObj = UnityHelper.CreateObject("ContextsHolder", null, UnityEngine.Vector3.zero);


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

public class NoSGUIMargin : AbstractGUIContext
{
    protected UnityEngine.Vector2 margin;

    public NoSGUIMargin(GUIAlignment alignment, UnityEngine.Vector2 margin) : base(alignment)
    {
        this.margin = margin;
    }

    public override GameObject? Instantiate(Size size, out Size actualSize)
    {
        actualSize = new(margin);
        return null;
    }
}


public class NebulaGUIContextEngine : Virial.Media.GUI
{
    public static NebulaGUIContextEngine Instance { get; private set; } = new();

    private Dictionary<AttributeParams, Virial.Text.TextAttribute> allAttr = new();
    private Dictionary<AttributeAsset, Virial.Text.TextAttribute> allAttrAsset = new();

    public Virial.Text.TextAttribute GetAttribute(AttributeParams attribute)
    {
        if (allAttr.TryGetValue(attribute, out var attr))
        {
            return attr;
        }
        else
        {
            var isFlexible = attribute.HasFlag(AttributeTemplateFlag.IsFlexible);
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
                AttributeAsset.OblongHeader => new Virial.Text.TextAttribute(Virial.Text.TextAlignment.Left, GetFont(FontAsset.Oblong), Virial.Text.FontStyle.Normal, new(5.2f,false), new(0.45f,3f), new(255,255,255), true),
                AttributeAsset.StandardMediumMasked => new Virial.Text.TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.6f, 0.8f, 1.6f), new(1.45f,0.3f), new(255,255,255), false),
                AttributeAsset.StandardLargeWideMasked => new Virial.Text.TextAttribute(Virial.Text.TextAlignment.Center, GetFont(FontAsset.Gothic), Virial.Text.FontStyle.Bold, new(1.7f, 1f, 1.7f), new(2.9f, 0.45f), new(255, 255, 255), false),
                _ => null!
            };
        }
        
        return allAttrAsset[attribute];
    }

    public Virial.Text.TextAttribute GenerateAttribute(AttributeParams attribute, Virial.Color color, FontSize fontSize, Size size)
    {
        Virial.Text.TextAlignment alignment =
            ((AttributeTemplateFlag)attribute & AttributeTemplateFlag.AlignmentMask) switch
            {
                AttributeTemplateFlag.AlignmentLeft => Virial.Text.TextAlignment.Left,
                AttributeTemplateFlag.AlignmentRight => Virial.Text.TextAlignment.Right,
                _ => Virial.Text.TextAlignment.Center
            };
        Virial.Text.Font font = GetFont(
            ((AttributeTemplateFlag)attribute & (AttributeTemplateFlag.FontMask | AttributeTemplateFlag.MaterialMask)) switch
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
            FontAsset.Gothic => new StaticFont(null, VanillaAsset.StandardTextPrefab.font),
            FontAsset.GothicMasked => new StaticFont(VanillaAsset.StandardMaskedFontMaterial, VanillaAsset.StandardTextPrefab.font),
            FontAsset.Oblong => new StaticFont(null, VanillaAsset.BrookFont),
            FontAsset.OblongMasked => new StaticFont(VanillaAsset.OblongMaskedFontMaterial, VanillaAsset.BrookFont),
            _ => new StaticFont(null, VanillaAsset.StandardTextPrefab.font),
        };
    }

    public GUIContext HorizontalHolder(GUIAlignment alignment, IEnumerable<GUIContext> inner, float? fixedHeight = null) => new HorizontalContextsHolder(alignment, inner) { FixedHeight = fixedHeight };

    public GUIContext VerticalHolder(GUIAlignment alignment, IEnumerable<GUIContext> inner, float? fixedWidth = null) => new VerticalContextsHolder(alignment, inner) { FixedWidth = fixedWidth };

    public GUIContext Image(GUIAlignment alignment, Image image, FuzzySize size) => new NoSGUIImage(alignment, image, size);

    public GUIContext ScrollView(GUIAlignment alignment, Size size, string? scrollerTag, GUIContext? inner, out Artifact<GUIScreen> artifact) {
        var result = new GUIScrollView(alignment, size.ToUnityVector(), inner) { ScrollerTag = scrollerTag, WithMask = true };
        artifact = result.Artifact;
        return result;
    }

    public GUIContext LocalizedButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string translationKey, Action onClick, Action? onMouseOver = null, Action? onMouseOut = null, Action? onRightClick = null, Virial.Color? color = null, Virial.Color? selectedColor = null)
        => new GUIButton(alignment, attribute, new TranslateTextComponent(translationKey)) { OnClick = onClick, OnMouseOver = onMouseOver, OnMouseOut = onMouseOut, OnRightClick = onRightClick, Color = color?.ToUnityColor(), SelectedColor = selectedColor?.ToUnityColor() };
    public GUIContext RawButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string rawText, Action onClick, Action? onMouseOver = null, Action? onMouseOut = null, Action? onRightClick = null, Virial.Color? color = null, Virial.Color? selectedColor = null)
        => new GUIButton(alignment, attribute, new RawTextComponent(rawText)) { OnClick = onClick, OnMouseOver = onMouseOver, OnMouseOut = onMouseOut, OnRightClick = onRightClick, Color = color?.ToUnityColor(), SelectedColor = selectedColor?.ToUnityColor() };
    public GUIContext Button(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text, Action onClick, Action? onMouseOver = null, Action? onMouseOut = null, Action? onRightClick = null, Virial.Color? color = null, Virial.Color? selectedColor = null)
        => new GUIButton(alignment, attribute, text) { OnClick = onClick, OnMouseOver = onMouseOver, OnMouseOut = onMouseOut, OnRightClick = onRightClick, Color = color?.ToUnityColor(), SelectedColor = selectedColor?.ToUnityColor() };


    public GUIContext LocalizedText(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string translationKey) => new NoSGUIText(alignment, attribute, new TranslateTextComponent(translationKey));
    
    public GUIContext RawText(GUIAlignment alignment, Virial.Text.TextAttribute attribute, string rawText) => new NoSGUIText(alignment, attribute, new RawTextComponent(rawText));
    
    public GUIContext Text(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text) => new NoSGUIText(alignment, attribute, text);
    

    public GUIContext Margin(FuzzySize margin) => new NoSGUIMargin(GUIAlignment.Center, new(margin.Width ?? 0f, margin.Height ?? 0f));

    public TextComponent TextComponent(Virial.Color color, string transrationKey) => TextComponent(color.ToUnityColor(), transrationKey);
    public TextComponent TextComponent(Color color, string transrationKey) => new ColorTextComponent(color, new TranslateTextComponent(transrationKey));
    public TextComponent RawTextComponent(string rawText) => new RawTextComponent(rawText);
    public TextComponent LocalizedTextComponent(string translationKey) => new TranslateTextComponent(translationKey);
    public TextComponent ColorTextComponent(Virial.Color color, TextComponent component) => new ColorTextComponent(color.ToUnityColor(), component);

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

    public void SetContext(GUIContext? context, out Size actualSize)
    {
        myScreen.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => GameObject.Destroy(obj)));

        if (context != null)
        {
            var obj = context.Instantiate(myAnchor, screenSize, out actualSize);
            if (obj != null) obj.transform.SetParent(myScreen.transform, false);
        }
        else
        {
            actualSize = new(0f, 0f);
        }
    }
}
using Nebula.Behavior;
using Virial.Compat;
using Virial.Media;

namespace Nebula.Modules.GUIWidget;


public class GUITextField : AbstractGUIWidget
{
    internal ListArtifact<TextField> Artifact = new();
    public Size FieldSize { get; init; }
    public bool IsSharpField { get; init; } = true;
    public float FontSize { get; init; } = 2f;
    public Predicate<char>? TextPredicate { get; init; } 
    public string HintText { get; init; } = "";
    public string DefaultText { get; init; } = "";
    public bool WithMaskMaterial { get; init; } = true;
    public int MaxLines { get; init; } = 1;
    public Predicate<string>? EnterAction { get; init; } = null;
    public GUITextField(GUIAlignment alignment, Size size) : base(alignment)
    {
        FieldSize = size;
    }

    internal override GameObject? Instantiate(Size size, out Size actualSize)
    {
        var unitySize = FieldSize.ToUnityVector();
        var obj = UnityHelper.CreateObject("TextField", null, UnityEngine.Vector3.zero);

        var field = UnityHelper.CreateObject<TextField>("Text", obj.transform, new UnityEngine.Vector3(0, 0, -0.1f));
        field.SetSize(unitySize, FontSize, MaxLines);
        field.InputPredicate = TextPredicate;
        field.EnterAction = EnterAction;
        if (WithMaskMaterial) field.AsMaskedText();

        var background = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
        background.sprite = IsSharpField ? NebulaAsset.SharpWindowBackgroundSprite.GetSprite() : VanillaAsset.TextButtonSprite;
        background.drawMode = SpriteDrawMode.Sliced;
        background.tileMode = SpriteTileMode.Continuous;
        background.size = unitySize;
        if (!IsSharpField) background.size += new UnityEngine.Vector2(0.12f, 0.12f);
        background.sortingOrder = 5;
        if (WithMaskMaterial) background.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var collider = background.gameObject.AddComponent<BoxCollider2D>();
        collider.size = unitySize;
        collider.isTrigger = true;
        var button = background.gameObject.SetUpButton(true, background);
        button.OnClick.AddListener(() => field.GainFocus());
        Artifact.Values.Add(field);
        if (DefaultText.Length > 0) field.SetText(DefaultText);
        if (HintText != null) field.SetHint(HintText!);
        
        actualSize = new Size(unitySize + new UnityEngine.Vector2(IsSharpField ? 0.1f : 0.22f, IsSharpField ? 0.1f : 0.22f));
        return obj;
    }
}


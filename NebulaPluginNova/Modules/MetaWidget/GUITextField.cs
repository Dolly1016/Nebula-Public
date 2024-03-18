using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Compat;
using Virial.Media;
using Virial.Text;
using static Il2CppSystem.Net.Http.Headers.Parser;
using static Nebula.Modules.IMetaWidgetOld;

namespace Nebula.Modules.MetaWidget;


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
        if (WithMaskMaterial) field.AsMaskedText();

        var background = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, UnityEngine.Vector3.zero);
        background.sprite = IsSharpField ? NebulaAsset.SharpWindowBackgroundSprite.GetSprite() : VanillaAsset.TextButtonSprite;
        background.drawMode = SpriteDrawMode.Sliced;
        background.tileMode = SpriteTileMode.Continuous;
        background.size = unitySize;
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

        actualSize = new Size(unitySize + new UnityEngine.Vector2(0.1f, 0.1f));
        return obj;
    }
}


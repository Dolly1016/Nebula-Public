using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.MetaContext;

public class GUIButton : NoSGUIText
{
    public Action? OnClick { get; init; }
    public Action? OnRightClick { get; init; }
    public Action? OnMouseOver { get; init; }
    public Action? OnMouseOut { get; init; }
    public Color? Color { get; init; }
    public Color? SelectedColor { get; init; }

    public string RawText { init { Text = new RawTextComponent(value); } }
    public string TranslationKey { init { Text = new TranslateTextComponent(value); } }
    public bool AsMaskedButton { get; init; }
    public float TextMargin { get; init; } = 0.26f;

    public GUIButton(GUIAlignment alignment, Virial.Text.TextAttribute attribute, TextComponent text) : base(alignment,attribute,text)
    {
        Attr = attribute;
        AsMaskedButton = attribute.Font.FontMaterial != null;
    }


    public override GameObject? Instantiate(Size size, out Size actualSize)
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
        if (OnClick != null) passiveButton.OnClick.AddListener(OnClick);
        if (OnMouseOut != null) passiveButton.OnMouseOut.AddListener(OnMouseOut);
        if (OnMouseOver != null) passiveButton.OnMouseOver.AddListener(OnMouseOver);

        if (OnRightClick != null) passiveButton.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked += OnRightClick;

        actualSize.Width += TextMargin + 0.1f;
        actualSize.Height += TextMargin + 0.1f;

        return button.gameObject;
    }
}

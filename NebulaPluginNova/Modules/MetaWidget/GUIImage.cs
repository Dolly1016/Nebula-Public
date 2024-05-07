using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.GUIWidget;

public class NoSGUIImage : AbstractGUIWidget
{
    protected Virial.Media.Image Image;
    protected FuzzySize Size;
    public Color? Color = null;
    public GUIClickableAction? OnClick;
    public GUIWidgetSupplier? Overlay;
    public bool IsMasked { get; init; }

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

        return renderer.gameObject;
    }
}
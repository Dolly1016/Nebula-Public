using Mono.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Compat;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.MetaContext;

public class NoSGUIImage : AbstractGUIContext
{
    protected Virial.Media.Image Image;
    protected FuzzySize Size;
    public Color? Color = null;
    public bool IsMasked { get; init; }

    public NoSGUIImage(GUIAlignment alignment, Virial.Media.Image image, FuzzySize size,Color? color = null) : base(alignment)
    {
        this.Image = image;
        this.Size = size;
        this.Color = color;
    }

    public override GameObject? Instantiate(Size size, out Size actualSize)
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

        return renderer.gameObject;
    }
}
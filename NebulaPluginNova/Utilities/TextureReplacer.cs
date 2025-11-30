using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nebula.Utilities;

internal class TextureReplacer
{
    ITextureLoader texture;
    List<(Tuple<int, int, int, int> rect, Image image)> images = [];

    public TextureReplacer(ITextureLoader texture)
    {
        this.texture = texture;
    }

    public Image GetImage(Rect rect, float pixelsPerUnit)
    {
        foreach (var entry in images)
        {
            if (
                entry.rect.Item1 == (int)rect.left &&
                entry.rect.Item2 == (int)rect.right &&
                entry.rect.Item3 == (int)rect.top &&
                entry.rect.Item4 == (int)rect.bottom
            )
            {
                return entry.image;
            }
        }
        var loader = new CacheSpriteLoader(() => texture.GetTexture().ToSprite(rect, pixelsPerUnit));
        images.Add((new((int)rect.left, (int)rect.right, (int)rect.top, (int)rect.bottom), loader));
        return loader;
    }

    public void ReplaceSprite(SpriteRenderer renderer)
    {
        renderer.sprite = Replace(renderer.sprite).GetSprite();
    }

    public Image Replace(Sprite original) => GetImage(original.rect, original.pixelsPerUnit);
}

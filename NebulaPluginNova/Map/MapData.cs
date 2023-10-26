using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nebula.Map;

public abstract class MapData
{
    abstract protected Vector2[] MapArea { get; }

    public bool CheckMapArea(Vector2 position)
    {
        int num = Physics2D.OverlapCircleNonAlloc(position, 0.23f, PhysicsHelpers.colliderHits, Constants.ShipAndAllObjectsMask);
        if (num > 0) for (int i = 0; i < num; i++) if (!PhysicsHelpers.colliderHits[i].isTrigger) return false;

        return CheckMapAreaInternal(position);
    }

    public bool CheckMapAreaInternal(Vector2 position)
    {
        Vector2 vector;
        float magnitude;

        foreach (Vector2 p in MapArea)
        {
            vector = p - position;
            magnitude = vector.magnitude;
            if (magnitude > 12.0f) continue;

            if (!PhysicsHelpers.AnyNonTriggersBetween(position, vector.normalized, magnitude, Constants.ShipAndAllObjectsMask)) return true;
        }

        return false;
    }

    public int CheckMapAreaDebug(Vector2 position)
    {
        Vector2 vector;
        float magnitude;
        int count = 0;
        foreach (Vector2 p in MapArea)
        {
            vector = p - position;
            magnitude = vector.magnitude;
            if (magnitude > 12.0f) continue;

            if (!PhysicsHelpers.AnyNonTriggersBetween(position, vector.normalized, magnitude, Constants.ShipAndAllObjectsMask)) count++;
        }

        return count;
    }

    private static Texture2D CreateReadabeTexture(Texture texture, int margin = 0)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

        Graphics.Blit(texture, renderTexture);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D readableTextur2D = new Texture2D(texture.width + margin * 2, texture.height + margin * 2);
        readableTextur2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), margin, margin);
        readableTextur2D.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);

        return readableTextur2D;
    }
    public Texture2D OutputMap(Vector2 pos, Vector2 size)
    {
        int x1, y1, x2, y2;
        x1 = (int)(pos.x * 10);
        y1 = (int)(pos.y * 10);
        x2 = x1 + (int)(size.x * 10);
        y2 = y1 + (int)(size.y * 10);
        int temp;
        if (x1 > x2)
        {
            temp = x1;
            x1 = x2;
            x2 = temp;
        }
        if (y1 > y2)
        {
            temp = y1;
            y1 = y2;
            y2 = temp;
        }

        Color color = new Color(40 / 255f, 40 / 255f, 40 / 255f);
        var texture = new Texture2D(x2 - x1, y2 - y1, TextureFormat.RGB24, false);

        int num;
        int r = 0;
        for (int y = y1; y < y2; y++)
        {
            for (int x = x1; x < x2; x++)
            {
                num = CheckMapAreaDebug(new Vector2(((float)x) / 10f, ((float)y) / 10f));

                texture.SetPixel(x - x1, y - y1, (num == 0) ? color : new Color((num > 1 ? 100 : 0) / 255f, (150 + (num * 5)) / 255f, 0));
                if (num > 0) r++;
            }
        }

        texture.Apply();

        return CreateReadabeTexture(texture);
    }

    static private MapData[] AllMapData = new MapData[] { new SkeldData(), new MiraData(), new PolusData(), null!, new AirshipData(), new FungleData() };
    static public MapData GetCurrentMapData() => AllMapData[AmongUsUtil.CurrentMapId];
}

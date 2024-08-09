using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Player;

internal class LightInfo
{
    static private List<LightInfo> allLightInfo = new();
    static public IEnumerable<LightInfo> AllLightInfo => allLightInfo;

    private SpriteRenderer lightRenderer;
    public bool IsValid => lightRenderer;
    public Vector2 Size => lightRenderer.transform.localScale * lightRenderer.size + new Vector2(0.8f, 0.8f);
    public LightInfo(SpriteRenderer lightRenderer)
    {
        this.lightRenderer = lightRenderer;
        allLightInfo.Add(this);
    }

    public static void UpdateLightInfo() => allLightInfo.RemoveAll(info => !info.IsValid);

    public bool CheckPoint(Vector2 point)
    {
        var vec = point - (Vector2)lightRenderer.transform.position;
        var size = Size;
        return Math.Sign(vec.x) < size.x && Math.Sign(vec.y) < size.y;
    }
}

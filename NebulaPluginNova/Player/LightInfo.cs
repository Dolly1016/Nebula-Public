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
    private Transform lightTransform;
    public bool IsValid => lightRenderer;
    public VVector2 Size => (VVector2)lightTransform.localScale * (VVector2)lightRenderer.size + new VVector2(0.8f, 0.8f);
    public LightInfo(SpriteRenderer lightRenderer)
    {
        this.lightRenderer = lightRenderer;
        this.lightTransform = lightRenderer.transform;
        allLightInfo.Add(this);
    }

    public static void UpdateLightInfo() => allLightInfo.RemoveAll(info => !info.IsValid);

    public bool CheckPoint(VVector2 point)
    {
        var vec = point - (VVector2)lightTransform.position;
        var size = Size;
        return Mathn.Sign(vec.x) < size.x && Mathn.Sign(vec.y) < size.y;
    }
}

using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Behavior;

internal class MinimapScaler : MonoBehaviour
{
    static MinimapScaler() => ClassInjector.RegisterTypeInIl2Cpp<MinimapScaler>();

    void Update()
    {
        var lossyScale = transform.lossyScale;
        var scale = transform.localScale;
        if (lossyScale.x < 0f) scale.x = -scale.x;
        if (lossyScale.y < 0f) scale.y = -scale.y;
        transform.localScale = scale;
    }
}

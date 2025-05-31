using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Behavior;

internal class SortingGroupOrderFixer : MonoBehaviour
{
    static SortingGroupOrderFixer() => ClassInjector.RegisterTypeInIl2Cpp<SortingGroupOrderFixer>();

    private Renderer renderer;
    private int groupOrder;
    public void LateUpdate()
    {
        if (renderer)
        {
            if (renderer.sortingGroupOrder != groupOrder) renderer.sortingGroupOrder = groupOrder;
        }
    }

    public void Initialize(Renderer renderer, int groupOrder)
    {
        this.renderer = renderer;
        this.groupOrder = groupOrder;
    }
}

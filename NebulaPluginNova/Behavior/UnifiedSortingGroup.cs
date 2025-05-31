using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;

namespace Nebula.Behavior;

internal class UnifiedSortingGroup: MonoBehaviour
{
    static UnifiedSortingGroup() => ClassInjector.RegisterTypeInIl2Cpp<UnifiedSortingGroup>();

    void Awake()
    {
        gameObject.AddComponent<SortingGroup>();
    }

    void Start()
    {
        foreach(var renderer in transform.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingOrder = 10;
        }
    }
}

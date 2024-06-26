﻿using Il2CppInterop.Runtime.Injection;
using UnityEngine.Rendering;

namespace Nebula.Behaviour;

public class ZOrderedSortingGroup : MonoBehaviour
{
    static ZOrderedSortingGroup() => ClassInjector.RegisterTypeInIl2Cpp<ZOrderedSortingGroup>();
    private SortingGroup group = null!;
    public void Start()
    {
        group = gameObject.AddComponent<SortingGroup>();
    }

    private float rate = 2000f;
    private int baseValue = 5;
    public void Update()
    {
        group.sortingOrder = baseValue - (int)(rate * transform.localPosition.z);
    }
}

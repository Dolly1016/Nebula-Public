using Il2CppInterop.Runtime.Injection;
using UnityEngine.Rendering;

namespace Nebula.Behavior;

public class ZOrderedSortingGroup : MonoBehaviour
{
    static ZOrderedSortingGroup() => ClassInjector.RegisterTypeInIl2Cpp<ZOrderedSortingGroup>();
    private SortingGroup? group = null;
    private Renderer? renderer = null;
    public int ConsiderParents = 0;
    public void SetConsiderParentsTo(Transform parent)
    {
        int num = 0;
        Transform t = transform;
        while(!(t == parent || t == null))
        {
            num++;
            t = t.parent;
        }
        ConsiderParents = num;
    }
    public void Start()
    {
        if(!gameObject.TryGetComponent<Renderer>(out renderer)) group = gameObject.AddComponent<SortingGroup>();
    }

    private float rate = 20000f;
    private int baseValue = 5;
    public void Update()
    {
        float z = transform.localPosition.z;
        Transform t = transform;
        for (int i = 0; i < ConsiderParents; i++)
        {
            t = t.parent;
            z += t.localPosition.z;
        }
        int layer = baseValue - (int)(rate * z);
        if (group != null)group.sortingOrder = layer;
        if(renderer != null) renderer.sortingOrder = layer;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

public class ObjectPool<T> where T : Component
{
    List<T> activatedObjects = new();
    List<T> inactivatedObjects = new();
    public Action<T>? OnInstantiated { get; set; }
    T original;
    Transform parent;

    public ObjectPool(T original, Transform parent){
        this.original = original;
        this.parent = parent;
    }

    public void DestroyAll()
    {
        foreach (var obj in activatedObjects) GameObject.Destroy(obj.gameObject);
        foreach (var obj in inactivatedObjects) GameObject.Destroy(obj.gameObject);
        activatedObjects.Clear();
        inactivatedObjects.Clear();
    }

    public T Instantiate()
    {
        if(inactivatedObjects.Count > 0)
        {
            T result = inactivatedObjects[inactivatedObjects.Count - 1];
            inactivatedObjects.RemoveAt(inactivatedObjects.Count - 1);

            activatedObjects.Add(result);
            result.gameObject.SetActive(true);
            return result;
        }
        else
        {
            T result = GameObject.Instantiate(original, parent);
            OnInstantiated?.Invoke(result);
            activatedObjects.Add(result);
            return result;
        }
    }

    public void RemoveAll()
    {
        foreach(var obj in activatedObjects)
        {
            obj.gameObject.SetActive(false);
            inactivatedObjects.Add(obj);
        }
        activatedObjects.Clear();
    }

    public void Inactivate(T obj)
    {
        activatedObjects.Remove(obj);
        inactivatedObjects.Add(obj);
        obj.gameObject.SetActive(false);
    }
}

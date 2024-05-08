namespace Nebula.Utilities;

public class ObjectPool<T> where T : Component
{
    List<T> activatedObjects = new();
    List<T> inactivatedObjects = new();
    public Action<T>? OnInstantiated { get; set; }
    Func<T> generator;
    Transform parent;

    public ObjectPool(T original, Transform parent){
        this.generator = () => GameObject.Instantiate(original, this.parent);
        this.parent = parent;
    }

    public ObjectPool(Func<Transform,T> generator, Transform parent)
    {
        this.generator = () => generator.Invoke(this.parent!);
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
            T result = generator.Invoke();
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

    public int Count => activatedObjects.Count;
}

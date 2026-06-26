namespace Nebula.Utilities;

public class ObjectPool<T> where T : Component
{
    private record ObjectEntry(T Val, int InstanceId, GameObject GameObj);
    List<ObjectEntry> activatedObjects = new();
    List<ObjectEntry> inactivatedObjects = new();
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
        foreach (var obj in activatedObjects) GameObject.Destroy(obj.GameObj);
        foreach (var obj in inactivatedObjects) GameObject.Destroy(obj.GameObj);
        activatedObjects.Clear();
        inactivatedObjects.Clear();
    }

    public T Instantiate()
    {
        if(inactivatedObjects.Count > 0)
        {
            var result = inactivatedObjects[inactivatedObjects.Count - 1];
            inactivatedObjects.RemoveAt(inactivatedObjects.Count - 1);

            activatedObjects.Add(result);
            result.GameObj.SetActive(true);
            return result.Val;
        }
        else
        {
            T result = generator.Invoke();
            var entry = new ObjectEntry(result, result.GetInstanceID(), result.gameObject);
            OnInstantiated?.Invoke(result);
            activatedObjects.Add(entry);
            return result;
        }
    }

    public void RemoveAll()
    {
        foreach(var obj in activatedObjects)
        {
            obj.GameObj.SetActive(false);
            inactivatedObjects.Add(obj);
        }
        activatedObjects.Clear();
    }

    public void Inactivate(T obj)
    {
        var instanceId = obj.GetInstanceID();
        var entry = activatedObjects.Find(entry => entry.InstanceId == instanceId);
        if (entry != null)
        {
            inactivatedObjects.Add(entry);
            entry.GameObj.SetActive(false);
        }
        else
        {
            GameObject.Destroy(obj.gameObject);
        }
    }

    public int Count => activatedObjects.Count;
}

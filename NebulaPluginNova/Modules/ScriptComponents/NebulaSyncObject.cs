using Virial;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;

public class NebulaSyncObjectReference
{
    int id;
    NebulaSyncObject? referTo = null;

    public NebulaSyncObjectReference(int id) {  this.id = id; }

    public NebulaSyncObject? SyncObject { get { 
            if(referTo == null) referTo = NebulaSyncObject.GetObject<NebulaSyncObject>(id);
            return referTo;
        }
    }
    public int ObjectId => id;
}

[NebulaRPCHolder]
public abstract class NebulaSyncObject : FlexibleLifespan, IGameOperator
{
    static private Dictionary<int, Func<float[], NebulaSyncObject>> instantiaters = new();
    static private Dictionary<int, NebulaSyncObject> allObjects = new();

    static protected void RegisterInstantiater(string tag, Func<float[], NebulaSyncObject> instantiater)
    {
        int hash = tag.ComputeConstantHash();
        if (instantiaters.ContainsKey(hash)) NebulaPlugin.Log.Print(NebulaLog.LogLevel.FatalError, $"Duplicated Instantiater Error ({tag})");
        instantiaters[hash] = instantiater;

        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"Sync Object \"{tag}\" has been registered. ({hash})");
    }

    public int ObjectId { get; private set; }
    public GamePlayer Owner { get; private set; } = null!;
    public bool AmOwner => Owner.AmOwner;
    private int TagHash { get; set; }

    private static int AvailableId(byte issuerId)
    {
        int idMask = issuerId << 16;
        while (true)
        {
            int cand = System.Random.Shared.Next(0xFFFF) | idMask;

            if (!allObjects.ContainsKey(cand)) return cand;
        }
    }
    public NebulaSyncObject()
    {
    }

    public virtual void OnInstantiated() { }
    public virtual void OnReleased() { 
        allObjects.Remove(ObjectId);
    }

    private float[] argumentsCache;

    static private byte OwnerIdFromObjectId(int objId) => (byte)(objId >>= 16);

    static private RemoteProcess<(int id,int tagHash, float[] arguments, bool skipLocal)> RpcInstantiateDef = new(
        "InstantiateObj",
        (message,local) =>
        {
            if (local && message.skipLocal) return;

            var obj = instantiaters[message.tagHash]?.Invoke(message.arguments);

            if (obj == null) return;

            obj.argumentsCache = message.arguments;
            obj.ObjectId = message.id;
            obj.Owner = NebulaGameManager.Instance!.GetPlayer(OwnerIdFromObjectId(obj.ObjectId))!;
            obj.TagHash = message.tagHash;
            if (allObjects.ContainsKey(obj.ObjectId)) throw new Exception("[NebulaSyncObject] Duplicated Key Error");
            obj.OnInstantiated();
            obj.Register(obj);
            allObjects.Add(obj.ObjectId, obj);
        });

    static private RemoteProcess<int> RpcDestroyDef = new("DestroyObj",
       (message, _) =>
       {
           if (allObjects.TryGetValue(message, out var obj)) obj.Release();
       });

    static public NebulaSyncObjectReference RpcInstantiate(string tag, float[]? arguments)
    {
        int id = AvailableId(PlayerControl.LocalPlayer.PlayerId);
        int hash = tag.ComputeConstantHash();
        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"Try Instantiate Sync GLOBAL Object (tag: {tag}, hash: {hash})");
        
        RpcInstantiateDef.Invoke(new(id, hash, arguments ?? Array.Empty<float>(), false));
        return new(id);
    }

    static public NebulaSyncObjectReference LocalInstantiate(string tag, float[]? arguments)
    {
        int id = AvailableId(PlayerControl.LocalPlayer.PlayerId);
        int hash = tag.ComputeConstantHash();
        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"Try Instantiate Sync LOCAL Object (tag: {tag}, hash: {hash})");
        RpcInstantiateDef.LocalInvoke(new(id, hash, arguments ?? Array.Empty<float>(), false));
        return new(id);
    }

    //ローカルでの生成を全体に反映させます。
    public void ReflectInstantiationGlobally()
    {
        RpcInstantiateDef.Invoke(new(ObjectId, TagHash, argumentsCache, true));
    }

    static public void RpcDestroy(int id)
    {
        RpcDestroyDef.Invoke(id);
    }

    static public void LocalDestroy(int id)
    {
        RpcDestroyDef.LocalInvoke(id);
    }

    static public T? GetObject<T>(int id) where T : NebulaSyncObject
    {
        if (allObjects.TryGetValue(id, out var obj)) return obj as T;
        return default(T);
    }

    static public IEnumerator<T> GetObjects<T>(string tag) where T : NebulaSyncObject
    {
        int hash = tag.ComputeConstantHash();
        foreach(var obj in allObjects.Values)
        {
            if (obj.TagHash != hash) continue;
            T? t = obj as T;
            if (t is not null) yield return t;
        }
    }
}

public class NebulaSyncStandardObject : NebulaSyncObject
{
    public enum ZOption
    {
        Back,
        Front,
        Just
    }

    public SpriteRenderer MyRenderer { get; private set; }

    public NebulaSyncStandardObject(Vector2 pos,ZOption zOrder,bool canSeeInShadow,Sprite sprite,Color color)
    {
        MyRenderer = UnityHelper.CreateObject<SpriteRenderer>("NebulaObject", null, pos, null);
        ZOrder = zOrder;
        CanSeeInShadow = canSeeInShadow;
        Sprite = sprite;
        Color = color;
    }

    public NebulaSyncStandardObject(Vector2 pos, ZOption zOrder, bool canSeeInShadow, Sprite sprite, bool semitransparent = false)
     : this(pos, zOrder, canSeeInShadow, sprite, semitransparent ? new Color(1, 1, 1, 0.5f) : Color.white) { }

    private ZOption zOrder;
    public ZOption ZOrder
    {
        get => zOrder;
        set {
            zOrder = value;
            Position = MyRenderer.transform.position;
        }
    }

    public Vector2 Position
    {
        get => MyRenderer.transform.position; 
        set {
            Vector3 pos = value;
            
            float z = value.y / 1000f;
            switch (ZOrder)
            {
                case ZOption.Back:
                    z += 0.001f;
                    break;
                case ZOption.Front:
                    z += -1f;
                    break;
            }

            pos.z = z;

            MyRenderer.transform.position = pos;
        }
    }

    public bool CanSeeInShadow
    {
        get => MyRenderer.gameObject.layer == LayerExpansion.GetObjectsLayer();
        set => MyRenderer.gameObject.layer = value ? LayerExpansion.GetObjectsLayer() : LayerExpansion.GetDefaultLayer();
    }

    public Sprite Sprite
    {
        get => MyRenderer.sprite;
        set => MyRenderer.sprite = value;
    }

    public Color Color
    {
        get => MyRenderer.color;
        set => MyRenderer.color = value;
    }

    public override void OnReleased()
    {
        base.OnReleased();
        if(MyRenderer) GameObject.Destroy(MyRenderer.gameObject);
    }

    protected static SystemConsole SystemConsolize(GameObject obj, SpriteRenderer? renderer, ImageNames image, Minigame minigamePrefab, float usableDistance = 1f)
    {
        obj.layer = LayerMask.NameToLayer("ShortObjects");
        PassiveButton button = obj.GetComponent<PassiveButton>();
        Collider2D collider = obj.GetComponent<Collider2D>();
        
        var console = obj.AddComponent<SystemConsole>();
        console.usableDistance = usableDistance;
        console.MinigamePrefab = minigamePrefab;
        console.useIcon = image;
        
        if (renderer != null)
        {
            console.Image = renderer;
            console.Image.material = VanillaAsset.GetHighlightMaterial();
        }


        if (!button)
        {
            button = obj.AddComponent<PassiveButton>();
            button.OnMouseOut = new UnityEngine.Events.UnityEvent();
            button.OnMouseOver = new UnityEngine.Events.UnityEvent();
            button._CachedZ_k__BackingField = 0.1f;
            button.CachedZ = 0.1f;
        }

        if (!collider)
        {
            var cCollider = obj.AddComponent<CircleCollider2D>();
            cCollider.radius = 0.4f;
            cCollider.isTrigger = true;
        }

        return console;
    }
}

public class NebulaSyncShadowObject : NebulaSyncStandardObject
{
    public SpriteRenderer ShadowRenderer { get; private set; }

    public NebulaSyncShadowObject(Vector2 pos, ZOption zOrder, Sprite sprite, Color color)
        :base(pos, zOrder, false, sprite)
    {
        ShadowRenderer = UnityHelper.CreateObject<SpriteRenderer>("NebulaObject", MyRenderer.transform, Vector3.zero, LayerExpansion.GetShadowLayer());
        ShadowSprite = sprite;
    }

    public Sprite ShadowSprite
    {
        get => ShadowRenderer.sprite;
        set => ShadowRenderer.sprite = value;
    }
}
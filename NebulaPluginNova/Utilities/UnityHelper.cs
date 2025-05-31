using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine.Video;

namespace Nebula.Utilities;

public static class ModSingleton<T> where T : class
{
    static public T Instance { get; set; } = null;
}

public static class UnityHelper
{

    public static GameObject CreateObject(string objName, Transform? parent, Vector3 localPosition,int? layer = null)
    {
        var obj = new GameObject(objName);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = new Vector3(1f, 1f, 1f);
        if (layer.HasValue) obj.layer = layer.Value;
        else if (parent != null) obj.layer = parent.gameObject.layer;
        return obj;
    }

    public static T CreateObject<T>(string objName, Transform? parent, Vector3 localPosition,int? layer = null) where T : Component
    {
        return CreateObject(objName, parent, localPosition, layer).AddComponent<T>();
    }

    //SortingGroupOrderを10に固定します。
    public static SpriteRenderer CreateSpriteRenderer(string objName, Transform? parent, Vector3 localPosition, int? layer = null)
    {
        var renderer = CreateObject<SpriteRenderer>(objName, parent, localPosition, layer);
        renderer.sortingGroupOrder = 10;
        return renderer;
    }

    public static void SetBothOrder(this Renderer renderer, int order)
    {
        renderer.sortingOrder = order;
        renderer.sortingGroupOrder = order;
    }

    public static (MeshRenderer renderer, MeshFilter filter) CreateMeshRenderer(string objName, Transform? parent, Vector3 localPosition, int? layer,Color? color = null,Material? material = null)
    {
        var meshFilter = UnityHelper.CreateObject<MeshFilter>("mesh", parent, localPosition, layer);
        var meshRenderer = meshFilter.gameObject.AddComponent<MeshRenderer>();
        if (!material)
        {
            meshRenderer.material = new Material(Shader.Find(color.HasValue ? "Unlit/Color" : "Unlit/Texture"));
            if (color.HasValue) meshRenderer.sharedMaterial.color = color.Value;
        }
        else
        {
            meshRenderer.material = material;
        }
        meshFilter.mesh = new Mesh();

        return (meshRenderer, meshFilter);
    }

    public static Camera CreateRenderingCamera(string objName, Transform? parent, Vector3 localPosition, float halfYSize, int layerMask = 31511) {
        var camera = UnityHelper.CreateObject<Camera>(objName, parent, localPosition);
        camera.backgroundColor = Color.black;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.depth = 5;
        camera.nearClipPlane = -1000f;
        camera.orthographic = true;
        camera.orthographicSize = halfYSize;
        camera.cullingMask = layerMask;
        return camera;
    }

    public static RenderTexture SetCameraRenderTexture(this Camera camera, int textureX, int textureY)
    {
        if (camera.targetTexture) GameObject.Destroy(camera.targetTexture);
        camera.targetTexture = new RenderTexture(textureX, textureY, 32, RenderTextureFormat.ARGB32);

        return camera.targetTexture;
    }

    public static VideoPlayer SetMovieToMesh(GameObject holder, MeshRenderer renderer, string url, bool loop)
    {
        holder.SetActive(false);
        var player = holder.AddComponent<VideoPlayer>();

        player.renderMode = VideoRenderMode.MaterialOverride;
        player.targetMaterialRenderer = renderer;
        player.source = VideoSource.Url;
        player.url = url;
        player.isLooping = loop;
        holder.SetActive(true);
        return player;
    }

    public static MeshFilter CreateRectMesh(this MeshFilter filter, Vector2 size, Vector3? center = null)
    {
        center ??= Vector3.zero;

        var mesh = filter.mesh;

        float x = size.x * 0.5f;
        float y = size.y * 0.5f;
        mesh.SetVertices((Vector3[])[
            new Vector3(-x , -y) + center.Value,
            new Vector3(x, -y) + center.Value,
            new Vector3(-x, y) + center.Value,
            new Vector3(x, y) + center.Value]);
        mesh.SetTriangles((int[])[0, 2, 1, 2, 3, 1], 0);
        mesh.SetUVs(0, (Vector2[])[new(0, 0), new(1, 0), new(0, 1), new(1, 1)]);
        var color = new Color32(255, 255, 255, 255);
        mesh.SetColors((Color32[])[color, color, color, color]);

        return filter;
    }

    public static LineRenderer SetUpLineRenderer(string objName,Transform? parent,Vector3 localPosition,int? layer = null,float width = 0.2f)
    {
        var line = UnityHelper.CreateObject<LineRenderer>(objName, parent, localPosition, layer);
        line.material.shader = Shader.Find("Sprites/Default");
        line.SetColors(Color.clear, Color.clear);
        line.positionCount = 2;
        line.SetPositions(new Vector3[] { Vector3.zero, Vector3.zero });
        line.useWorldSpace = false;
        line.SetWidth(width, width);
        line.alignment = LineAlignment.View;
        
        return line;
    }

    static public Transform? TryDig(this Transform transform, params string[] objectName)
    {
        foreach(var name in objectName)
        {
            if (transform == null || !transform) return null;
            transform = transform.FindChild(name);
        }
        return transform;
    }

    public static T? FindAsset<T>(string name) where T : Il2CppObjectBase
    {
        foreach (var asset in UnityEngine.Object.FindObjectsOfTypeIncludingAssets(Il2CppType.Of<T>()))
        {
            if (asset.name == name) return asset.Cast<T>();
        }
        return null;
    }

    public static T MarkDontUnload<T>(this T obj) where T : UnityEngine.Object
    {
        GameObject.DontDestroyOnLoad(obj);
        obj.hideFlags |= HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;

        return obj;
    }

    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : MonoBehaviour => gameObject.TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();
    
    public static Camera? FindCamera(int cameraLayer) => Camera.allCameras.FirstOrDefault(c => (c.cullingMask & (1 << cameraLayer)) != 0);

    public static Vector3 ScreenToWorldPoint(Vector3 screenPos, int cameraLayer)
    {
        return FindCamera(cameraLayer)?.ScreenToWorldPoint(screenPos) ?? Vector3.zero;
    }

    public static Vector3 ScreenToLocalPoint(Vector3 screenPos, int cameraLayer, Transform transform)
    {
        return transform.InverseTransformPoint(ScreenToWorldPoint(screenPos, cameraLayer));
    }

    public static Vector3 WorldToScreenPoint(Vector3 worldPos, int cameraLayer)
    {
        return FindCamera(cameraLayer)?.WorldToScreenPoint(worldPos) ?? Vector3.zero;
    }


    public static PassiveButton SetUpButton(this GameObject gameObject, bool withSound = false, SpriteRenderer? buttonRenderer = null, Color? defaultColor = null, Color? selectedColor = null)
        => SetUpButton(gameObject, withSound, buttonRenderer != null ? [buttonRenderer] : [], defaultColor, selectedColor);

    public static PassiveButton SetUpButton(this GameObject gameObject, bool withSound, SpriteRenderer[] buttonRenderers, Color? defaultColor = null, Color? selectedColor = null) {
        var button = gameObject.AddComponent<PassiveButton>();
        button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.OnMouseOut = new UnityEngine.Events.UnityEvent();
        button.OnMouseOver = new UnityEngine.Events.UnityEvent();

        if (withSound)
        {
            button.OnClick.AddListener(VanillaAsset.PlaySelectSE);
            button.OnMouseOver.AddListener(VanillaAsset.PlayHoverSE);
        }
        if (buttonRenderers.Length > 0)
        {
            button.OnMouseOut.AddListener(() => { foreach (var r in buttonRenderers) r.color = defaultColor ?? Color.white; });
            button.OnMouseOver.AddListener(() => { foreach (var r in buttonRenderers) r.color = selectedColor ?? Color.green; });
        }

        if (buttonRenderers.Length > 0) foreach(var r in buttonRenderers)r.color = defaultColor ?? Color.white;
        
        return button;
    }

    static public void AddListener(this UnityEngine.UI.Button.ButtonClickedEvent onClick, Action action) => onClick.AddListener((UnityEngine.Events.UnityAction)action);
    static public void AddListener(this UnityEngine.Events.UnityEvent unityEvent, Action action) => unityEvent.AddListener((UnityEngine.Events.UnityAction)action);

    public static void SetModText(this TextTranslatorTMP text,string translationKey)
    {
        text.TargetText = (StringNames)short.MaxValue;
        text.defaultStr = translationKey;
    }

    static public void DoTransitionFade(this TransitionFade transitionFade, GameObject? transitionFrom, GameObject? transitionTo, Action onTransition, Action callback)
    {
        if (transitionTo) transitionTo!.SetActive(false);

        IEnumerator Coroutine()
        {
            yield return Effects.ColorFade(transitionFade.overlay, Color.clear, Color.black, 0.1f);
            if (transitionFrom && transitionFrom!.gameObject) transitionFrom.gameObject.SetActive(false);
            if (transitionTo && transitionTo!.gameObject) if (transitionTo != null) transitionTo.gameObject.SetActive(true);
            onTransition.Invoke();
            yield return null;
            yield return Effects.ColorFade(transitionFade.overlay, Color.black, Color.clear, 0.1f);
            callback.Invoke();
            yield break;
        }

        transitionFade.StartCoroutine(Coroutine().WrapToIl2Cpp());
    }

    public static IEnumerator HandleException(this IEnumerator enumerator, Action<Exception> action)
    {
        StackfullCoroutine coroutine = new(enumerator);
        while (true)
        {
            bool hasNext;

            try
            {
                hasNext = coroutine.MoveNext();
            }
            catch (Exception e)
            {
                action.Invoke(e);
                hasNext = false;
            }

            if (!hasNext) break;

            yield return null;
        }
        yield break;
    }

    public static void ForEachAllChildren(this GameObject gameObject,Action<GameObject> todo)
    {
        gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => {
            todo.Invoke(obj);
            obj.ForEachAllChildren(todo);
        }));
    }

    public static void ForEachChild(this GameObject gameObject, Action<GameObject> todo)
    {
        gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => {
            todo.Invoke(obj);
        }));
    }

    public static Vector3 AsVector3(this Vector2 vec,float z)
    {
        Vector3 result = vec;
        result.z = z;
        return result;
    }

    public static Material GetMeshRendererMaterial() => new(NebulaAsset.MeshRendererShader);
    public static Material GetMeshRendererMaskedMaterial() => new(NebulaAsset.MeshRendererMaskedShader);

    /// <summary>
    /// 自分自身と、すべての子に対して手続きを実行します。
    /// </summary>
    /// <param name="obj"></param>
    public static void DoForAllChildren(GameObject obj, Action<GameObject> procedure, bool doSelf = true)
    {
        if(doSelf) procedure.Invoke(obj);

        void _sub__DoForAllChildren(GameObject parent)
        {
            for(int i = 0;i < parent.transform.childCount; i++)
            {
                var child = parent.transform.GetChild(i).gameObject;
                procedure.Invoke(child);
                _sub__DoForAllChildren(child);
            }
        }

        _sub__DoForAllChildren(obj);
    }

    public static SpriteRenderer SimpleAnimator(Transform? parent, Vector3 localPos, float spf, Func<int, Sprite> sprite, int? layer = null)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Animator", parent, localPos, layer);
        renderer.sprite = sprite.Invoke(0);

        System.Collections.IEnumerator CoUpdate()
        {
            int num = 0;
            do
            {
                yield return Effects.Wait(spf);
                num++;
                if(renderer) renderer.sprite = sprite.Invoke(num);
            } while (renderer);
        }
        NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
        return renderer;
    }
}


using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using TMPro;
using Twitch;
using UnityEngine;

namespace Nebula;

public class VanillaAsset
{
    public class VanillaAudioClip
    {
        private string name;
        private AudioClip? clip = null;
        public AudioClip Clip { get
            {
                if (clip) return clip;
                clip = UnityHelper.FindAsset<AudioClip>(name);
                return clip!;
            } }

        public VanillaAudioClip(string name)
        {
            this.name = name;
        }
    }
    static public Sprite PopUpBackSprite { get; private set; } = null!;
    static public Sprite FullScreenSprite { get; private set; } = null!;
    static public Sprite TextButtonSprite { get; private set; } = null!;
    static public Sprite CloseButtonSprite { get; private set; } = null!;
    static public TMPro.TextMeshPro StandardTextPrefab { get; private set; } = null!;
    static public VanillaAudioClip HoverClip { get; private set; } = new("UI_Hover");
    static public VanillaAudioClip SelectClip { get; private set; } = new("UI_Select");
    static public VanillaAudioClip HnSTransformClip { get; private set; } = new("HnS_ImpostorScream");
    static public Material StandardMaskedFontMaterial { get {
            if (standardMaskedFontMaterial == null) standardMaskedFontMaterial = UnityHelper.FindAsset<Material>("LiberationSans SDF - BlackOutlineMasked")!;
            return standardMaskedFontMaterial!;
        }
    }
    static public Material OblongMaskedFontMaterial { get { 
            if(oblongMaskedFontMaterial == null) oblongMaskedFontMaterial = UnityHelper.FindAsset<Material>("Brook Atlas Material Masked");
            return oblongMaskedFontMaterial!;
        } }
    
    static private Material? standardMaskedFontMaterial = null;
    static private Material? oblongMaskedFontMaterial = null;

    static private TMP_FontAsset? versionFont = null;
    static public TMP_FontAsset VersionFont
    {
        get
        {
            if (versionFont == null) versionFont = UnityHelper.FindAsset<TMP_FontAsset>("Barlow-Medium SDF");
            return versionFont!;
        }
    }

    static private TMP_FontAsset? preSpawnFont = null;
    static public TMP_FontAsset PreSpawnFont { get
        {
            if(preSpawnFont==null) preSpawnFont = UnityHelper.FindAsset<TMP_FontAsset>("DIN_Pro_Bold_700 SDF")!;
            return preSpawnFont;
        }
    }

    static private TMP_FontAsset? brookFont = null;
    static public TMP_FontAsset BrookFont
    {
        get
        {
            if (brookFont == null) brookFont = UnityHelper.FindAsset<TMP_FontAsset>("Brook SDF")!;
            return brookFont;
        }
    }

    static public PlayerCustomizationMenu PlayerOptionsMenuPrefab { get; private set; } = null!;

    static public readonly ShipStatus[] MapAsset = new ShipStatus[6];
    static public Vector2 GetMapCenter(byte mapId) => MapAsset[mapId].MapPrefab.transform.GetChild(5).localPosition;
    static public float GetMapScale(byte mapId) => VanillaAsset.MapAsset[mapId].MapScale;
    static public Vector2 ConvertToMinimapPos(Vector2 pos,Vector2 center, float scale)=> (pos / scale) + center;
    static public Vector2 ConvertToMinimapPos(Vector2 pos, byte mapId) => ConvertToMinimapPos(pos, GetMapCenter(mapId), GetMapScale(mapId));
    static public Vector2 ConvertFromMinimapPosToWorld(Vector2 minimapPos, Vector2 center, float scale) => (minimapPos - center) * scale;
    static public Vector2 ConvertFromMinimapPosToWorld(Vector2 minimapPos, byte mapId) => ConvertFromMinimapPosToWorld(minimapPos, GetMapCenter(mapId), GetMapScale(mapId));

    static public void LoadAssetAtInitialize()
    {   
        PlayerOptionsMenuPrefab = UnityHelper.FindAsset<PlayerCustomizationMenu>("LobbyPlayerCustomizationMenu")!;
    }

    public static void PlaySelectSE() => SoundManager.Instance.PlaySound(SelectClip.Clip, false, 0.8f);
    public static void PlayHoverSE() => SoundManager.Instance.PlaySound(HoverClip.Clip, false, 0.8f);

    static public IEnumerator CoLoadAssetOnTitle()
    {
        var twitchPopUp = TwitchManager.Instance.transform.GetChild(0);
        PopUpBackSprite = twitchPopUp.GetChild(3).GetComponent<SpriteRenderer>().sprite;
        TextButtonSprite = twitchPopUp.GetChild(2).GetComponent<SpriteRenderer>().sprite;
        FullScreenSprite = twitchPopUp.GetChild(0).GetComponent<SpriteRenderer>().sprite;
        CloseButtonSprite = UnityHelper.FindAsset<Sprite>("closeButton")!;
        

        StandardTextPrefab = GameObject.Instantiate(twitchPopUp.GetChild(1).GetComponent<TMPro.TextMeshPro>(),null);
        StandardTextPrefab.gameObject.hideFlags = HideFlags.HideAndDontSave;
        GameObject.Destroy(StandardTextPrefab.spriteAnimator);
        GameObject.DontDestroyOnLoad(StandardTextPrefab.gameObject);

        while (AmongUsClient.Instance == null) yield return null;


        //AsyncOperationHandle<GameObject> handle;
        //AmongUsClient.Instance.ShipPrefabs[2].RuntimeKey;
        //UnityEngine.AddressableAssets.Addressables.LoadAssetAsync(AmongUsClient.Instance.ShipPrefabs[0].RuntimeKey, null, false, false);
        for (int i = 0; i < MapAsset.Length; i++) {
            if (i == 3) continue;
            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(AmongUsClient.Instance.ShipPrefabs[i].RuntimeKey);
            yield return handle;
            MapAsset[i] = handle.Result.GetComponent<ShipStatus>();
        }

        //マップの部屋名フォントを読み込んだうえで再度フォントを適用
        Language.ReflectFallBackFont();

        //Polus
        //handle = AmongUsClient.Instance.ShipPrefabs[2].InstantiateAsync(null, false);
        //yield return handle;
        //var polus = handle.Result.GetComponent<PolusShipStatus>();


        /*
        //Airship
        handle = AmongUsClient.Instance.ShipPrefabs[4].InstantiateAsync(null, false);
        yield return handle;
        */

        yield break;
    }

    static public Scroller GenerateScroller(Vector2 size, Transform transform, Vector3 scrollBarLocalPos, Transform target, FloatRange bounds, float scrollerHeight)
    {
        var barBack = GameObject.Instantiate(PlayerOptionsMenuPrefab.transform.GetChild(4).FindChild("UI_ScrollbarTrack").gameObject, transform);
        var bar = GameObject.Instantiate(PlayerOptionsMenuPrefab.transform.GetChild(4).FindChild("UI_Scrollbar").gameObject, transform);
        barBack.transform.localPosition = scrollBarLocalPos + new Vector3(0.12f, 0f, 0f);
        bar.transform.localPosition = scrollBarLocalPos;

        var scrollBar = bar.GetComponent<Scrollbar>();

        var scroller = UnityHelper.CreateObject<Scroller>("Scroller", transform, new Vector3(0, 0, 5));
        scroller.gameObject.AddComponent<BoxCollider2D>().size = size;

        scrollBar.parent = scroller;
        scrollBar.graphic = bar.GetComponent<SpriteRenderer>();
        scrollBar.trackGraphic = barBack.GetComponent<SpriteRenderer>();
        scrollBar.trackGraphic.size = new Vector2(scrollBar.trackGraphic.size.x, scrollerHeight);

        var ratio = scrollerHeight / 3.88f;

        scroller.Inner = target;
        scroller.SetBounds(bounds, null);
        scroller.allowY = true;
        scroller.allowX = false;
        scroller.ScrollbarYBounds = new FloatRange(-1.8f * ratio + scrollBarLocalPos.y + 0.4f, 1.8f * ratio + scrollBarLocalPos.y - 0.4f);
        scroller.ScrollbarY = scrollBar;
        scroller.active = true;
        //scroller.Colliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider2D>(new Collider2D[] { hitBox });

        scroller.ScrollToTop();

        return scroller;
    }

    private static Material? highlightMaterial = null;
    public static Material GetHighlightMaterial()
    {
        if (highlightMaterial != null) return new Material(highlightMaterial);
        foreach (var mat in UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
        {
            if (mat.name == "HighlightMat")
            {
                highlightMaterial = mat.TryCast<Material>();
                break;
            }
        }
        return new Material(highlightMaterial);
    }

    public static PlayerDisplay GetPlayerDisplay()
    {
        AmongUsClient.Instance.PlayerPrefab.gameObject.SetActive(false);
        var display = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab.gameObject);
        AmongUsClient.Instance.PlayerPrefab.gameObject.SetActive(true);

        GameObject.Destroy(display.GetComponent<PlayerControl>());
        GameObject.Destroy(display.GetComponent<PlayerPhysics>());
        GameObject.Destroy(display.GetComponent<Rigidbody2D>());
        GameObject.Destroy(display.GetComponent<CircleCollider2D>());
        GameObject.Destroy(display.GetComponent<CustomNetworkTransform>());
        GameObject.Destroy(display.GetComponent<BoxCollider2D>());
        GameObject.Destroy(display.GetComponent<DummyBehaviour>());
        GameObject.Destroy(display.GetComponent<AudioSource>());
        GameObject.Destroy(display.GetComponent<PassiveButton>());
        GameObject.Destroy(display.GetComponent<HnSImpostorScreamSfx>());

        display.gameObject.SetActive(true);

        GameObject.Destroy(display.GetComponent<UncertifiedPlayer>());
        display.GetComponentInChildren<NebulaCosmeticsLayer>().RejectZOrdering = true;
        return display.AddComponent<PlayerDisplay>();
    }
}

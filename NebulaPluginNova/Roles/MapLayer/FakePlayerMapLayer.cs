using Il2CppInterop.Runtime.Injection;
using Nebula.Map;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nebula.Roles.Impostor.Sculptor;

namespace Nebula.Roles.MapLayer;

internal class FakePlayerMapLayer : MonoBehaviour
{
    static FakePlayerMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<FakePlayerMapLayer>();

    private Camera camera;
    private PassiveButton clickButton;
    private CircleCollider2D collider;
    private SpriteRenderer cameraDotRenderer = null!, dotRenderer = null!;
    static private readonly Image whiteCircleSprite = SpriteLoader.FromResource("Nebula.Resources.WhiteCircle.png", 100f);
    protected float CurrentMapScale;
    protected Vector2 CurrentMapCenter;

    protected virtual void Awake()
    {
        collider = UnityHelper.CreateObject<CircleCollider2D>("Click", transform, new(0f, 0f, -5f));
        collider.radius = 2f;
        collider.isTrigger = true;

        dotRenderer = UnityHelper.CreateObject<SpriteRenderer>("DotRenderer", transform, new(0f, 0f, -25f));
        dotRenderer.sprite = whiteCircleSprite.GetSprite();
        dotRenderer.color = Color.green;
        dotRenderer.transform.localScale = Vector3.one * 0.45f;

        clickButton = collider.gameObject.SetUpButton(false);

        clickButton.OnMouseOver.AddListener(() =>
        {
            NebulaManager.Instance.SetHelpWidget(clickButton,
            new NoSGameObjectGUIWrapper(Virial.Media.GUIAlignment.Center, () =>
            {
                var mesh = UnityHelper.CreateMeshRenderer("MeshRenderer", transform, new(0, -0.08f, -1), null);
                mesh.filter.CreateRectMesh(new(2f, 1.2f));
                mesh.renderer.sharedMaterial.mainTexture = camera.SetCameraRenderTexture(200, 120);
                mesh.renderer.transform.localScale = MapBehaviourExtension.GetMinimapFlippedScale();
                return (mesh.renderer.gameObject, new(2f, 1.2f));
            }), true);
        });
        clickButton.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(clickButton));

        void TryCommandHere()
        {
            var screenPosAsWorld = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer());
            var worldPosOnMinimap = transform.InverseTransformPoint(screenPosAsWorld);
            worldPosOnMinimap.z = -5f;
            var worldPos = VanillaAsset.ConvertFromMinimapPosToWorld(worldPosOnMinimap, AmongUsUtil.CurrentMapId);

            bool inMap = MapData.GetCurrentMapData().CheckMapArea(worldPos, 0.2f);
            if (inMap) OnClick(worldPos, worldPosOnMinimap);
        }


        clickButton.OnClick.AddListener(TryCommandHere);
        camera = UnityHelper.CreateRenderingCamera("DoppelgangerCamera", null, Vector3.zero, 1.6f, LayerExpansion.GetLayerMask(LayerExpansion.GetDefaultLayer(), LayerExpansion.GetObjectsLayer(), LayerExpansion.GetShortObjectsLayer(), LayerExpansion.GetShipLayer()));
        cameraDotRenderer = UnityHelper.CreateObject<SpriteRenderer>("DotImage", camera.transform, new(0f, 0f, -10f), LayerExpansion.GetDefaultLayer());
        cameraDotRenderer.sprite = whiteCircleSprite.GetSprite();

        CurrentMapCenter = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
        CurrentMapScale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);
    }
    protected virtual void Update()
    {
        //z: -10くらいのところに閉じるボタンがあるので、背景のクリックガードは-5くらいに置けばよい
        // 背景クリックガード :-5, 線及び点のクリック: -10
        // EdgeCollider2D.EdgeRadiousが使えそう

        var screenPosAsWorld = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer());
        var worldPosOnMinimap = transform.InverseTransformPoint(screenPosAsWorld);
        var worldPos = VanillaAsset.ConvertFromMinimapPosToWorld(worldPosOnMinimap, AmongUsUtil.CurrentMapId);
        camera.transform.position = worldPos;

        worldPosOnMinimap.z = -5f;
        collider.transform.localPosition = worldPosOnMinimap;

        worldPosOnMinimap.z = -25f;
        dotRenderer.transform.localPosition = worldPosOnMinimap;

        bool onMap = MapData.GetCurrentMapData().CheckMapArea(worldPos, 0.2f);
        //collider.gameObject.SetActive(onMap);
        dotRenderer.color = onMap ? Color.green : Color.red;
    }

    void OnDestroy()
    {
        if (camera) GameObject.Destroy(camera.gameObject);
    }

    void OnEnable()
    {
        if (cameraDotRenderer) cameraDotRenderer.enabled = true;
    }

    virtual protected void OnDisable()
    {
        if (cameraDotRenderer) cameraDotRenderer.enabled = false;
    }

    virtual protected void OnClick(Vector2 worldPos, Vector2 minimapPos) { }
}
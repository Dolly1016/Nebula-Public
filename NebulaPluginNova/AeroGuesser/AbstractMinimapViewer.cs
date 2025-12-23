using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;
using Virial.Text;

namespace Nebula.AeroGuesser;

internal abstract class AbstractMinimapViewer
{
    GameObject minimapHolder;
    GameObject scaler;
    SpriteRenderer minimapRenderer;

    protected GameObject Holder => minimapHolder;
    protected GameObject MapScaler => scaler;
    protected SpriteRenderer MinimapRenderer => minimapRenderer;

    static private readonly MultiImage pinImage = DividedSpriteLoader.FromResource("Nebula.Resources.MapPin.png", 100f, 2, 1);
    protected AbstractMinimapViewer(Transform parent)
    {
        SetUpBase(parent);
    }

    private void SetUpBase(Transform parent)
    {
        //ホルダ　ゲームの進行に伴う左右移動、上下移動を担う
        minimapHolder = UnityHelper.CreateObject("MinimapHolder", parent, new(0f, 0f, -10f));

        float scalerQ = 0f;
        scaler = UnityHelper.CreateObject("Scaler", minimapHolder.transform, new(0f, 0f, 0f));

        //ミニマップ
        minimapRenderer = UnityHelper.CreateObject<SpriteRenderer>("Minimap", scaler.transform, new(0f, 0f, 0f));
        minimapRenderer.material = VanillaAsset.GetMapMaterial();
        minimapRenderer.color = VanillaAsset.MapBlue;

        OnSetUp();
    }

    protected abstract void OnSetUp();

    float percentWidth = 1f;
    byte currentMapId = byte.MaxValue;
    public byte CurrentMapId => currentMapId;
    public void SetMap(byte mapId)
    {
        if (currentMapId == mapId)
        {
            OnSetMap(mapId, false);
            return;
        }
        currentMapId = mapId;

        var map = VanillaAsset.MapAsset[mapId].MapPrefab;
        minimapRenderer.sprite = VanillaAsset.GetMinimapRenderer(mapId).sprite;
        percentWidth = map.GetComponent<AspectSize>().PercentWidth;

        //マップごとの微調整
        if (mapId == 0) percentWidth *= 0.9f;
        if (mapId == 5) percentWidth *= 1.18f;

        minimapRenderer.transform.localScale = new(percentWidth, percentWidth, 1f);

        OnSetMap(mapId, true);
    }

    protected abstract void OnSetMap(byte mapId, bool changed);

    protected Vector2 LocalToRealPos(Vector2 localPos) => VanillaAsset.ConvertFromMinimapPosToWorld(localPos / MinimapRenderer.transform.localScale.x, currentMapId);
    protected Vector2 RealToLocalPos(Vector2 realPos) => VanillaAsset.ConvertToMinimapPos(realPos, currentMapId) * MinimapRenderer.transform.localScale.x;
    protected Vector2 ScreenToLocalPos(Vector2 screenPos) => scaler.transform.InverseTransformPoint(HudManager.Instance.UICamera.ScreenToWorldPoint(screenPos));

    protected SpriteRenderer CreatePin(Vector2 localPos, int? colorId = null)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Pin", MapScaler.transform, localPos.AsVector3(-5f));
        renderer.sprite = pinImage.GetSprite(colorId.HasValue ? 1 : 0);
        if (colorId.HasValue)
        {
            renderer.material = HatManager.Instance.PlayerMaterial;
            PlayerMaterial.SetColors(colorId.Value, renderer);
        }
        return renderer;
    }

}


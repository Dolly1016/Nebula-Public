using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nebula.Behavior;

internal class CustomShadow : MonoBehaviour
{
    static CustomShadow() => ClassInjector.RegisterTypeInIl2Cpp<CustomShadow>();

    public SpriteRenderer Renderer;
    public SpriteRenderer MulRenderer;
    public SpriteRenderer DefaultRenderer;
    public Func<bool>? Predicate = null;
    public float MulCoeffForSurvivers = 0f;
    public float MulCoeffForGhosts = 1f;
    public ShadowCollab ShadowCollab;
    private bool IsActive => ShadowCollab.ShadowQuad.gameObject.active && !(GamePlayer.LocalPlayer?.EyesightIgnoreWalls ?? false) && (Predicate?.Invoke() ?? true);
    void LateUpdate()
    {
        UpdateProperty();

        if (!IsActive)
        {
            Renderer.enabled = false;
            return;
        }
        Renderer.enabled = true;

        //影カメラのビュー射影行列を渡し、シェーダ側でワールド座標から影テクスチャを参照させる。
        //メッシュのUVに依存しないため、Tiled/分割スプライト/回転していても影がずれない。
        var cam = ShadowCollab.ShadowCamera;
        Renderer.material.SetMatrix(ShadowVPId, cam.projectionMatrix * cam.worldToCameraMatrix);
        Renderer.material.SetFloat(BlendId, 1f);
    }

    private static readonly int ShadowVPId = Shader.PropertyToID("_ShadowVP");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    /// <summary>
    /// 影を構成する全てのレンダラの描画モードを切り替えます。
    /// Tiled/Slicedを正しく描画するため、スプライトはFullRectで生成されている必要があります。
    /// </summary>
    /// <param name="drawMode">描画モード</param>
    /// <param name="size">描画サイズ (Tiled/Slicedのとき有効)</param>
    /// <param name="tileMode">タイルの敷き詰め方 (Tiledのとき有効)</param>
    public void SetDrawMode(SpriteDrawMode drawMode, Vector2 size, SpriteTileMode tileMode = SpriteTileMode.Continuous)
    {
        foreach (var r in new SpriteRenderer[] { Renderer, MulRenderer, DefaultRenderer })
        {
            r.drawMode = drawMode;
            r.tileMode = tileMode;
            r.size = size;
        }
    }

    /// <summary>
    /// Tiled/Sliced描画時のサイズを変更します。
    /// </summary>
    public void SetSize(Vector2 size)
    {
        Renderer.size = size;
        MulRenderer.size = size;
        DefaultRenderer.size = size;
    }

    private float lastBlend = 0f;
    public void SetBlend(float blend)
    {
        lastBlend = blend;
        UpdateProperty();
    }

    private float lastAlpha = 1f;
    public void SetAlpha(float alpha)
    {
        lastAlpha = alpha;
        UpdateProperty();
    }

    private void UpdateProperty()
    {
        bool isActive = IsActive;
        Renderer.material.SetColor("_Color", Color.white.AlphaMultiplied(lastAlpha * lastBlend));
        DefaultRenderer.color = Color.white.AlphaMultiplied(lastAlpha * (1f - lastBlend));
        MulRenderer.color = Color.white.AlphaMultiplied(lastAlpha 
            * (isActive ? MulCoeffForSurvivers : MulCoeffForGhosts)
            * lastBlend
            );
    }

    public void SetSprite(Sprite sprite)
    {
        Renderer.sprite = sprite;
        MulRenderer.sprite = sprite;
        DefaultRenderer.sprite = sprite;
    }
}

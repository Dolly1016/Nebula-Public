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

        var cam = ShadowCollab.ShadowCamera;
        //最初は半分の大きさ
        var height = cam.orthographicSize;
        var width = height * cam.aspect;
        Vector2 orig = cam.transform.position;
        orig.x -= width;
        orig.y -= height;
        //2倍にしておく
        height *= 2f;
        width *= 2f;

        float ConvertX(float x) => (x - orig.x) / width;
        float ConvertY(float y) => (y - orig.y) / height;

        var spriteWidthHalf = Renderer.sprite.rect.width / Renderer.sprite.pixelsPerUnit * 0.5f * Renderer.transform.localScale.x;
        var spriteHeightHalf = Renderer.sprite.rect.height / Renderer.sprite.pixelsPerUnit * 0.5f * Renderer.transform.localScale.y;
        var rendererX = Renderer.transform.position.x;
        var rendererY = Renderer.transform.position.y;
        Renderer.material.SetVector("_ShadowRange", new(
            ConvertX(rendererX - spriteWidthHalf),
            ConvertX(rendererX + spriteWidthHalf),
            ConvertY(rendererY - spriteHeightHalf),
            ConvertY(rendererY + spriteHeightHalf)
            ));
        Renderer.material.SetFloat("_Blend", 1f);
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

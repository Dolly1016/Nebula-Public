﻿using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Behaviour;

public class EffectCircle : MonoBehaviour
{
    SpriteRenderer? outerCircle, innerCircle;
    public Func<float>? OuterRadius, InnerRadius;
    private Color color;
    public Color Color { get => color; set
        {
            if (outerCircle) outerCircle!.color = value;
            if (innerCircle) innerCircle!.color = value;
            color = value;
        }
    }

    static EffectCircle() => ClassInjector.RegisterTypeInIl2Cpp<EffectCircle>();
    static private ISpriteLoader innerCircleSprite = SpriteLoader.FromResource("Nebula.Resources.EffectCircleInner.png",100f);
    static private ISpriteLoader outerCircleSprite = SpriteLoader.FromResource("Nebula.Resources.EffectCircle.png", 250f);
    private bool isActive = true;

    public void Update()
    {
        if (isActive)
        {
            if (OuterRadius == null)
            {
                if (outerCircle)
                {
                    GameObject.Destroy(outerCircle!.gameObject);
                    outerCircle = null;
                }
            }
            else
            {
                if (outerCircle == null)
                {
                    outerCircle = UnityHelper.CreateObject<SpriteRenderer>("OuterCircle", transform, new Vector3(0, 0, -10f), LayerExpansion.GetDefaultLayer());
                    outerCircle.sprite = outerCircleSprite.GetSprite();
                    outerCircle.color = Color;
                }

                var scale = outerCircle.transform.localScale.x;
                scale -= (scale - OuterRadius.Invoke()) * Time.deltaTime * 3.6f;
                outerCircle.transform.localScale = Vector3.one * scale;
            }

            if (InnerRadius == null)
            {
                if (innerCircle)
                {
                    GameObject.Destroy(innerCircle!.gameObject);
                    innerCircle = null;
                }
            }
            else
            {
                if (innerCircle == null)
                {
                    innerCircle = UnityHelper.CreateObject<SpriteRenderer>("InnerCircle", transform, new Vector3(0, 0, -10f), LayerExpansion.GetDefaultLayer());
                    innerCircle.sprite = innerCircleSprite.GetSprite();
                    innerCircle.color = Color;
                }

                var scale = innerCircle.transform.localScale.x;
                scale -= (scale - InnerRadius.Invoke()) * Time.deltaTime * 3.6f;
                innerCircle.transform.localScale = Vector3.one * scale;
            }
        }
    }


    public void Disappear()
    {
        if (!isActive) return;

        IEnumerator CoDisappear()
        {
            isActive = false;

            var c = Color;
            var origAlpha = c.a;
            var p = 1f;
            while (p > 0f)
            {
                c.a = origAlpha * p;
                if (innerCircle) innerCircle!.color = c;
                if (outerCircle) outerCircle!.color = c;
                p -= Time.deltaTime * 1.4f;
                yield return null;
            }

            GameObject.Destroy(gameObject);
        }

        StartCoroutine(CoDisappear().WrapToIl2Cpp());
    }

}

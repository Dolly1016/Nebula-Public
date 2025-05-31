using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behavior;

public class EffectCircle : MonoBehaviour
{
    SpriteRenderer? outerCircle, innerCircle;
    public Func<float>? OuterRadius, InnerRadius;
    public Vector2? TargetLocalPos = null;
    private Color color;
    public Color Color { get => color; set
        {
            if (outerCircle) outerCircle!.color = value;
            if (innerCircle) innerCircle!.color = value;
            color = value;
        }
    }

    static EffectCircle() => ClassInjector.RegisterTypeInIl2Cpp<EffectCircle>();
    static private Image innerCircleSprite = SpriteLoader.FromResource("Nebula.Resources.EffectCircleInner.png",100f);
    static private Image outerCircleSprite = SpriteLoader.FromResource("Nebula.Resources.EffectCircle.png", 250f);
    static public Image OuterCircleImage => outerCircleSprite;
    private bool isActive = true;

    public void Update()
    {
        if (!isActive && TargetLocalPos.HasValue) transform.localPosition = TargetLocalPos.Value.AsVector3(transform.localPosition.z);
        if (isActive)
        {
            if (TargetLocalPos.HasValue)
            {
                transform.localPosition -= ((Vector2)transform.localPosition - TargetLocalPos.Value).Delta(5.2f,0.015f).AsVector3(0f);
            }

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
                    outerCircle.transform.localScale = Vector3.zero;
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
                    innerCircle.transform.localScale = Vector3.zero;
                }

                var scale = innerCircle.transform.localScale.x;
                scale -= (scale - InnerRadius.Invoke()) * Time.deltaTime * 3.6f;
                innerCircle.transform.localScale = Vector3.one * scale;
            }
        }
    }

    public void DestroyFast()
    {
        GameObject.Destroy(gameObject);
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

    static public EffectCircle SpawnEffectCircle(Transform? parent, Vector3 localPos, Color color, float? outerRadious, float? innerRadious, bool ignoreShadow)
    {
        var circle = UnityHelper.CreateObject<EffectCircle>("Circle", parent, localPos, ignoreShadow ? LayerExpansion.GetDefaultLayer() : LayerExpansion.GetDefaultLayer());
        circle.Color = color;
        if(outerRadious != null) circle.OuterRadius = () => outerRadious.Value;
        if (innerRadious != null) circle.InnerRadius = () => innerRadious.Value;
        var script = circle.gameObject.AddComponent<ScriptBehaviour>();
        return circle;
    }
}

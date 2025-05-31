
namespace Nebula.Utilities;

public static class ManagedEffects
{
    //actionはt=0,1の両方の場合で必ず実行されます。
    static public IEnumerator Lerp(float duration, Action<float> action)
    {
        float t = 0f;
        while(t < duration)
        {
            action.Invoke(t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        action.Invoke(1f);
    }

    static public void Do(this IEnumerator coroutine)
    {
        while (coroutine.MoveNext()) { }
    }

    static public IEnumerator Sequence(params IEnumerator[] enumerator)
    {
        foreach(var e in enumerator) yield return e;
    }

    static public IEnumerator ToCoroutine(this Action action)
    {
        action.Invoke();
        yield break;
    }

    static public IEnumerator Action(Action action)
    {
        action.Invoke();
        yield break;
    }
    static public IEnumerator DelayedAction(Action action)
    {
        yield return null;
        action.Invoke();
        yield break;
    }

    static public IEnumerator Wait(float second)
    {
        var t = Time.time;
        while (Time.time - t < second) yield return null;
        yield break;
    }

    static public IEnumerator Wait(Func<bool> waitWhile, Action then)
    {
        while (waitWhile()) yield return null;
        then.Invoke();
        yield break;
    }

    static public IEnumerator WaitAsCoroutine(this Task task)
    {
        while (!task.IsCompleted) yield return null;
        yield break;
    }

    static public IEnumerator WaitAsCoroutine<T>(this Task<T> task)
    {
        while (!task.IsCompleted) yield return null;
        yield break;
    }

    static public IEnumerator BeamSequence(int width, params IEnumerator[] coroutines)
    {
        IEnumerator[] current = new IEnumerator[width];
        int next = 0;
        while (true)
        {
            bool hasCoroutine = next < coroutines.Length;
            for (int i = 0; i < width; i++)
            {
                if (current[i] != null) {
                    if( !current[i].MoveNext()) current[i] = null!;
                    else hasCoroutine = true;
                }
                if (current[i] == null && next < coroutines.Length) current[i] = current[i] = coroutines[next++];
            }
            if (!hasCoroutine) break;
            yield return null;
        }
    }

    static public IEnumerator Smooth(this Transform transform, Vector3 goalLocalPosition, float duration)
    {
        float p = 0f;
        var origin = transform.localPosition;
        while(p < 1f)
        {
            p += Time.deltaTime / duration;

            float pp = (1f - p) * (1f - p);
            pp *= pp * pp;
            transform.localPosition = origin * pp + goalLocalPosition * (1f - pp);
            yield return null;
        }

        transform.localPosition = goalLocalPosition;
    }

    private static IEnumerator CoPlayEffect(int layer, string name, Image sprite, Transform? parent, Vector3 pos, Vector3 velocity, float angVel, float scale, Color color, float maxTime, float fadeInTime, float fadeOutTime)
    {
        var obj = new GameObject(name);
        if (parent != null) obj.transform.SetParent(parent);
        obj.transform.localPosition = pos;
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        obj.transform.localEulerAngles = new Vector3(0, 0, System.Random.Shared.NextSingle() * 360f);
        obj.layer = layer;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite.GetSprite();

        float p = 0f;
        while (p < maxTime)
        {
            obj.transform.localPosition += velocity * Time.deltaTime;
            obj.transform.eulerAngles += new Vector3(0, 0, angVel * Time.deltaTime);

            float c = 1f;
            if (fadeInTime > 0f && p < fadeInTime) c = Math.Clamp(p / fadeInTime, 0f, 1f);
            else if (fadeOutTime > 0f) c = Math.Clamp((maxTime - p) / fadeOutTime, 0f, 1f);

            renderer.color = color.AlphaMultiplied(c);
            p += Time.deltaTime;
            yield return null;
        }
        GameObject.Destroy(obj);
    }

    private static Image smokeSprite = SpriteLoader.FromResource("Nebula.Resources.Smoke.png", 100f);
    public static IEnumerator CoSmokeEffect(int layer, Transform? parent, Vector3 pos, Vector3 velocity, float angVel, float scale)
    {
        return CoPlayEffect(layer, "Smoke", smokeSprite, parent, pos, velocity, angVel, scale, Color.white, 0.4f, 0f, 0.35f);
    }

    public static IEnumerator CoDisappearEffect(int layer, Transform? parent, Vector3 pos, float scale = 1f)
    {
        var obj = new GameObject("DisappearEffect");
        if (parent != null) obj.transform.SetParent(parent);
        obj.transform.localPosition = pos;
        obj.transform.localScale = new Vector3(scale, scale, 1f);

        List<IEnumerator> coroutines = [];
        
        //円を描くように7つの煙を配置
        for (int i = 0; i < 7; i++)
        {
            coroutines.Add(CoSmokeEffect(layer, obj.transform, new Vector3(0.4f, 0f).RotateZ(360f / 7f * (float)i),
                new Vector3(System.Random.Shared.NextSingle() * 0.4f + 0.1f, System.Random.Shared.NextSingle() * -0.1f).RotateZ(System.Random.Shared.NextSingle() * 360f),
                System.Random.Shared.NextSingle() * 40, 0.35f + System.Random.Shared.NextSingle() * 0.1f));
        }
        //ランダムに配置
        for (int i = 0; i < 4; i++)
        {
            coroutines.Add(CoSmokeEffect(layer, obj.transform,
                 new Vector3(System.Random.Shared.NextSingle() * 0.3f, 0f).RotateZ(System.Random.Shared.NextSingle() * 360f),
                new Vector3(System.Random.Shared.NextSingle() * 0.4f + 0.1f, System.Random.Shared.NextSingle() * -0.1f).RotateZ(System.Random.Shared.NextSingle() * 360f),
                System.Random.Shared.NextSingle() * 40, 0.35f + System.Random.Shared.NextSingle() * 0.1f));
        }

        yield return Effects.All(coroutines.Select(r => r.WrapToIl2Cpp()).ToArray());
        GameObject.Destroy(obj);
    }

    public static IEnumerator CoPlayAnimEffect(int layer, string name, IDividedSpriteLoader sprite, Transform? parent, Vector3 pos, float scale, Color color, float spf, bool flip = false)
    {
        var obj = new GameObject(name);
        if (parent != null) obj.transform.SetParent(parent);
        obj.transform.localPosition = pos;
        obj.transform.localScale = new Vector3(scale * (flip ? -1f : 1f), scale, 1f);
        obj.transform.localEulerAngles = new Vector3(0, 0, System.Random.Shared.NextSingle() * 360f);
        obj.layer = layer;
        var renderer = obj.AddComponent<SpriteRenderer>();

        int i = 0;
        while (i < sprite.Length)
        {
            renderer.sprite = sprite.GetSprite(i++);
            yield return Effects.Wait(spf);
        }

        GameObject.Destroy(obj);
    }
}

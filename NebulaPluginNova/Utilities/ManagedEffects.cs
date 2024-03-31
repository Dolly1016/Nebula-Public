using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

public static class ManagedEffects
{
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

    static public IEnumerator WaitAll(this IEnumerable<IEnumerator?> enumerators)
    {
        var coroutines = enumerators.Where(e => e != null).Select(e=>e.WrapToIl2Cpp()).ToArray();
        yield return Effects.All(coroutines);
    }

    static public IEnumerator Smooth(this Transform transform, Vector3 goalLocalPosition, float duration)
    {
        float p = 0f;
        var origin = transform.localPosition;
        while(p < 1f)
        {
            p += Time.deltaTime / duration;

            float pp = (1f - p) * (1f - p);
            transform.localPosition = origin * pp + goalLocalPosition * (1f - pp);
            yield return null;
        }

        transform.localPosition = goalLocalPosition;
    }
}

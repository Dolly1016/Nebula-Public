using BepInEx.Unity.IL2CPP.Utils.Collections;
using System.Collections;

namespace Virial.Helpers;

static public class CoroutineHelper
{
    static public IEnumerator HighSpeedEnumerator(this IEnumerator enumerator)
    {
        IEnumerator? currentCs = enumerator;
        Il2CppSystem.Collections.IEnumerator? currentIl = null;

        Stack<object> stack = new Stack<object>();

        while (true)
        {
            if (currentCs?.MoveNext() ?? currentIl?.MoveNext() ?? false)
            {
                object? next = currentCs?.Current ?? currentIl?.Current;
                if (next != null)
                {
                    stack.Push(((object?)currentCs ?? (object?)currentIl)!);
                    currentCs = null;
                    currentIl = null;
                    if (next is IEnumerator e1)
                        currentCs = e1;
                    else if (next is Il2CppSystem.Collections.IEnumerator e2)
                        currentIl = e2;

                    continue;
                }
                else
                {
                    yield return null;
                }
            }
            else
            {
                if (stack.Count == 0)
                    yield break;
                else
                {
                    var last = stack.Pop();
                    if (last is IEnumerator e1)
                        currentCs = e1;
                    else if (last is Il2CppSystem.Collections.IEnumerator e2)
                        currentIl = e2;

                    continue;
                }
            }
        }
    }

    static public IEnumerator WaitAll(this IEnumerable<IEnumerator?> enumerators)
    {
        var coroutines = enumerators.Where(e => e != null).Select(e => e.WrapToIl2Cpp()).ToArray();
        var coroutine = Effects.All(coroutines);
        coroutine.MoveNext();
        yield return coroutine;
    }
}

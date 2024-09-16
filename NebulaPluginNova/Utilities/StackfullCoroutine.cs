namespace Nebula.Utilities;

public class StackfullCoroutine
{
    private List<IEnumerator> stack = new();

    public StackfullCoroutine(IEnumerator enumerator)
    {
        stack.Add(enumerator);
    }

    public bool MoveNext() {
        if (stack.Count == 0) return false;

        var current = stack[stack.Count - 1];
        if (!current.MoveNext())
            stack.RemoveAt(stack.Count - 1);
        else if (current.Current != null)
        {
            if (current.Current is IEnumerator child)
                stack.Add(child);
            else if (current.Current is Il2CppSystem.Collections.IEnumerator il2CppChild)
                stack.Add(il2CppChild.WrapToManaged());
        }

        return stack.Count > 0;
    }

    public void Wait()
    {
        while (MoveNext()) { }
    }

    //状態を共有している点に注意
    public IEnumerator AsEnumerator()
    {
        while (MoveNext()) yield return null;
    }
}

public class ParallelCoroutine
{
    StackfullCoroutine[] coroutines;
    bool someoneFinished = false;
    public ParallelCoroutine(params StackfullCoroutine[] coroutines)
    {
        this.coroutines = coroutines;
    }

    public bool MoveNext()
    {
        bool allFinished = true;
        foreach(var c in coroutines)
        {
            bool result = c.MoveNext();
            someoneFinished |= !result;
            allFinished &= !result;
        }

        return !allFinished;
    }

    //状態を共有している点に注意
    public IEnumerator AsEnumerator()
    {
        while (MoveNext()) yield return null;
    }

    //ただ待機するだけのコルーチン。処理自体は別の誰かが担う必要がある。
    public IEnumerator JustWaitSomeoneFinished()
    {
        while (!someoneFinished) yield return null;
    }

    public IEnumerator WaitAndProcessTillSomeoneFinished()
    {
        while (!someoneFinished)
        {
            MoveNext();
            yield return null;
        }
    }

    public bool SomeoneFinished => someoneFinished;
}

public static class ManagedCoroutineHelper
{
    static public StackfullCoroutine AsStackfullCoroutine(this IEnumerator enumerator) => new(enumerator);
    static public StackfullCoroutine Continue(Func<bool> func)
    {
        IEnumerator CoWait()
        {
            while (func()) yield return null;
        }
        return CoWait().AsStackfullCoroutine();
    }
}
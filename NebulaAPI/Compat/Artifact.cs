using System.Collections;

namespace Virial.Compat;

public interface Reference<T>
{
    T Value { get; }
}

public interface Artifact<T> : IEnumerable<T>
{
    void Do(Action<T> action);
}

internal class ListArtifact<T> : Artifact<T>
{
    internal List<T> Values { get; init; } = new();

    public ListArtifact()
    {
        
    }

    public void Do(Action<T> action)
    {
        foreach(var t in Values) action.Invoke(t);
    }

    public IEnumerator<T> GetEnumerator() => Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>Values.GetEnumerator();
}

internal class GeneralizedArtifact<T,V> : Artifact<T> where V : T where T : class
{
    internal Artifact<V> Inner { get; init; }

    public GeneralizedArtifact(Artifact<V> inner)
    {
        Inner = inner;
    }

    public void Do(Action<T> action) => Inner.Do(v => action(v));

    public IEnumerator<T> GetEnumerator() => Inner.Select(v => v as T).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Inner.Select(v => v as T).GetEnumerator();
}

internal class WrappedArtifact<T, V> : Artifact<T>
{
    internal Artifact<V> Inner { get; init; }
    private Func<V,T> converter { get; init; }
    public WrappedArtifact(Artifact<V> inner, Func<V, T> converter)
    {
        Inner = inner;
        this.converter = converter;
    }

    public void Do(Action<T> action) => Inner.Do(v => action(converter.Invoke(v)));

    public IEnumerator<T> GetEnumerator() => Inner.Select(v => converter.Invoke(v)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Inner.Select(v => converter.Invoke(v)).GetEnumerator();
}
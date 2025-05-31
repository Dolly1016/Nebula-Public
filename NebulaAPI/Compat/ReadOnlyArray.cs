using System.Collections;

namespace Virial.Compat;

/// <summary>
/// 配列のスライスを表します。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IReadOnlyArray<T> : IEnumerable<T>, IReadOnlyList<T>
{
    IReadOnlyArray<T> Skip(int skipped);
    IReadOnlyArray<T> Slice(int start, int length);

    public static IReadOnlyArray<T> Empty() => new ReadOnlyArray<T>(Array.Empty<T>());
} 

/// <summary>
/// 配列のスライスです。
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReadOnlyArray<T> : IReadOnlyArray<T>
{
    private T[] list;
    private (int start, int length) span;

    public ReadOnlyArray(IEnumerable<T>? enumerable, int start = 0, int length = -1) : this(enumerable?.ToArray() ?? [], start, length) { }

    internal ReadOnlyArray(T[] list, int start = 0, int length = -1)
    {
        if (length == -1) length = list.Length - start;
        span = (start, length);
        this.list = list;
    }

    internal ReadOnlyArray(ReadOnlyArray<T> list, int start = 0, int length = -1)
    {
        if (length == -1) length = list.Count - start;
        span = (list.span.start + start, length);
        this.list = list.list;
    }

    public IReadOnlyArray<T> Skip(int skipped) => new ReadOnlyArray<T>(this, skipped);
    public IReadOnlyArray<T> Slice(int start, int length) => new ReadOnlyArray<T>(this, start, length);

    public T this[int index]
    {
        get => this.list[span.start + index];
    }

    public int Count => span.length;

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < span.length; i++) yield return list[span.start + i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

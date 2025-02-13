using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Helpers;

public static class EnumerableHelper
{
    static public IEnumerable<T> Delimit<T>(this IEnumerable<T> enumerable, T delimiter)
    {
        bool isFirst = true;
        foreach (T item in enumerable)
        {
            if (!isFirst) yield return delimiter;
            yield return item;
            isFirst = false;
        }
    }

    static public IEnumerable<T> Smooth<T>(this IEnumerable<IEnumerable<T>> enumerable)
    {
        foreach (var nested in enumerable) foreach (var e in nested) yield return e;
    }

    /// <summary>
    /// リストの先頭から調べ、最初に発見した要素を1つ削除します。
    /// RemoveAllとは後続に条件に適うものがあっても削除されない点が異なります。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    static public T? RemoveFirst<T>(this List<T> list, Predicate<T> predicate)
    {
        int index = list.FindIndex(predicate);
        if (index == -1) return default;

        var removed = list[index];
        list.RemoveAt(index);
        return removed;
    }

    static public bool Find<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate, [MaybeNullWhen(false)] out T found)
    {
        found = enumerable.FirstOrDefault(predicate);
        return found != null;
    }

    static public int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (predicate.Invoke(list[i])) return i;
        }
        return -1;
    }
}

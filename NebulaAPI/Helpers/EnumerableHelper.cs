using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Helpers;

public static class EnumerableHelper
{
    /// <summary>
    /// Joinの別名です。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <param name="delimiter"></param>
    /// <returns></returns>
    [Obsolete("Use Join instead.")]
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

    /// <summary>
    /// 二重のコレクションを1つのコレクションに展開します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <returns></returns>
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

    /// <summary>
    /// 先頭から、条件に適う要素を発見します。
    /// Tがnullを許容しない場合、default値が返ることに注意してください。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <param name="predicate"></param>
    /// <param name="found"></param>
    /// <returns></returns>
    static public bool Find<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate, [MaybeNullWhen(false)] out T found)
    {
        found = enumerable.FirstOrDefault(predicate);
        return found != null;
    }

    /// <summary>
    /// 条件に適う最初の要素の位置を返します。発見できない場合は-1を返します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    static public int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (predicate.Invoke(list[i])) return i;
        }
        return -1;
    }

    /// <summary>
    /// コレクションの各要素の間にセパレータを挿入します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerator"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    static public IEnumerable<T> Join<T>(this IEnumerable<T> enumerator, T separator)
    {
        bool isFirst = true;
        foreach (var item in enumerator)
        {
            if (isFirst)
                isFirst = false;
            else
                yield return separator;
            yield return item;
        }
    }

    /// <summary>
    /// コレクションの各要素の間に異なるセパレータを挿入します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerator"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    static public IEnumerable<T> Join<T>(this IEnumerable<T> enumerator, Func<T> separator)
    {
        bool isFirst = true;
        foreach (var item in enumerator)
        {
            if (isFirst)
                isFirst = false;
            else
                yield return separator.Invoke();
            yield return item;
        }
    }

    /// <summary>
    /// 多重なコレクションの各要素の間にセパレータを挿入し、展開します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerator"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    static public IEnumerable<T> JoinMany<T>(this IEnumerable<IEnumerable<T>> enumerator, T separator)
    {
        bool isFirst = true;
        foreach (var item in enumerator)
        {
            if (isFirst)
                isFirst = false;
            else
                yield return separator;
            foreach (var subItem in item) yield return subItem;
        }
    }

    /// <summary>
    /// 多重なコレクションの各要素の間に異なるセパレータを挿入し、展開します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerator"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    static public IEnumerable<T> Join<T>(this IEnumerable<IEnumerable<T>> enumerator, Func<T> separator)
    {
        bool isFirst = true;
        foreach (var item in enumerator)
        {
            if (isFirst)
                isFirst = false;
            else
                yield return separator.Invoke();
            foreach (var subItem in item) yield return subItem;
        }
    }

    /// <summary>
    /// コレクションが空か調べます。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <returns></returns>
    static public bool IsEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.Any(_ => true);

    /// <summary>
    /// コレクションが空か調べます。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    static public bool IsEmpty<T>(this IReadOnlyList<T> list) => list.Count == 0;

    /// <summary>
    /// nullでない場合、アクションを実行します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="nullableObj"></param>
    /// <param name="action"></param>
    static public void DoIf<T>(this T? nullableObj, Action<T> action)
    {
        if (nullableObj != null) action.Invoke(nullableObj!);
    }
}

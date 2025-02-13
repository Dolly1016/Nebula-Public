using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public class OrderedList<T, Key> : IEnumerable<T>, IReadOnlyCollection<T>
{
    public static OrderedList<T, int> AscendingList(Func<T, int> keySelector) => new OrderedList<T, int>(keySelector, (n1, n2) => n1 - n2);
    public static OrderedList<T, int> DescendingList(Func<T, int> keySelector) => new OrderedList<T, int>(keySelector, (n1, n2) => n2 - n1);

    private LinkedList<T> list;
    private Func<T, Key> keySelector;
    /// <summary>
    /// 左の値の方が小さければ負の値を返す関数。
    /// 昇順に並べます。
    /// </summary>
    private Func<Key, Key, int> keyComparer;
    public void Add(T item)
    {
        Key key = keySelector.Invoke(item);

        var node = list.First;
        while(node != null)
        {
            Key tKey = keySelector.Invoke(node.Value);
            if(keyComparer.Invoke(tKey, key) > 0)
            {
                list.AddBefore(node, item);
                return;
            }
            node = node.Next;
        }

        list.AddLast(item);
    }

    public bool Remove(T item) => list.Remove(item);

    public OrderedList(Func<T, Key> keySelector, Func<Key, Key, int> keyComparer)
    {
        this.list = [];
        this.keySelector = keySelector;
        this.keyComparer = keyComparer;
    }

    public int Count => list.Count;
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
}

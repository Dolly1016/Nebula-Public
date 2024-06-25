using System;
using System.Collections.Generic;
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
}

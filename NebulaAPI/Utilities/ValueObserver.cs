using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public class ValueObserver<T> where T : struct
{
    static private readonly EqualityComparer<T> comparer = EqualityComparer<T>.Default;

    private T value;
    private Action<T> onUpdate;

    public ValueObserver(T value, Action<T> onUpdate, bool requireInitialize = false)
    {
        this.value = value;
        this.onUpdate = onUpdate;
        if (requireInitialize) this.onUpdate(value);
    }

    public void Set(T value)
    {
        if(comparer.Equals(this.value, value)) return;
        this.value = value;
        this.onUpdate.Invoke(value);
    }
}

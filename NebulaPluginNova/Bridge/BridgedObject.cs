using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Bridge;

internal class BridgedObject<T> 
{
    bool cached;
    T value;
    Func<T> getter;
    public BridgedObject(Func<T> value)
    {
        this.cached = false;
        this.getter = value;
    }

    public T Get()
    {
        if (cached) return value;
        value = getter.Invoke();
        cached = true;
        return value;
    }
}

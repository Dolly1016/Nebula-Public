using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public class Cache<T> where T : class
{
    T? value { get; set; } = null;
    Func<T> getter;
    public Cache(Func<T> getter)
    {
        this.getter = getter;
    }

    public T Get()
    {
        value ??= getter.Invoke();
        return value;
    }

    public static implicit operator Cache<T>(Func<T> getter) => new Cache<T>(getter);
    public static implicit operator T(Cache<T> cache) => cache.Get();
}

public class ComponentCache<T> where T : UnityEngine.Component
{
    T? value { get; set; } = null;
    Func<T> getter;
    public ComponentCache(Func<T> getter)
    {
        this.getter = getter;
    }

    public T Get()
    {
        if(!value) value = getter.Invoke();
        return value;
    }

    public static implicit operator ComponentCache<T>(Func<T> getter) => new ComponentCache<T>(getter);
}

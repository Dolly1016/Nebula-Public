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
    
}

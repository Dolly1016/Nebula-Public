using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Utilities;

public class TimelimitedCache<T>
{
    private float time;
    private Func<T> func;
    private T cache;
    private bool hasValue;
    private float duration;
    public TimelimitedCache(Func<T> value, float duration = 0.1f)
    {
        this.time = -10f;
        this.func = value;
        this.cache = default!;
        this.hasValue = false;
        this.duration = duration;
    }

    public T Value { get
        {
            if (!hasValue || (Time.time > duration + time))
            {
                cache = func.Invoke();
                time = Time.time;
                hasValue = true;
            }
            return cache;
        }
    }
}

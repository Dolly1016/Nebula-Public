using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Compat;

namespace Nebula.Utilities;

public class PassiveObserver<T> : Virial.Utilities.IObserver<T> where T : notnull, IEquatable<T>
{
    private Func<T> getter;
    private Action<T> setter;
    private Action<T> onUpdate;
    private T lastCachedVal;

    public PassiveObserver(Func<T> getter, Action<T> setter, Action<T> onUpdate)
    {
        this.getter = getter;
        this.setter = setter;
        this.lastCachedVal = getter.Invoke();
        this.onUpdate = onUpdate;
    }

    public T Value => getter.Invoke();

    private void CacheCurrentValue()
    {
        lastCachedVal = Value;
    }
    private void CheckAndInvokeCallback()
    {
        if(lastCachedVal.Equals(Value)) return;
        OnUpdate(Value);
        CacheCurrentValue();
    }

    public void SetValue(T value)
    {
        setter.Invoke(value);
        CheckAndInvokeCallback();
    }

    public PassiveObserver<T> SubscribeEvent<E>(ILifespan lifespan, Func<E, bool>? filter = null) where E : Virial.Events.Event
    {
        GameOperatorManager.Instance?.Subscribe<E>(e => {
            if (filter?.Invoke(e) ?? true) CheckAndInvokeCallback();
        }, lifespan);
        return this;
    }

    public void OnUpdate(T newValue) => onUpdate.Invoke(newValue);
}
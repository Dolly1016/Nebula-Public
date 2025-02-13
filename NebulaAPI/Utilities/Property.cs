using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Utilities;

public interface IFunctionalGetProperty<T> { 
    public T Value { get; }
}

public interface IFunctionalSetProperty<T>
{
    public T Value { set; }
}

public interface IFunctionalProperty<T> : IFunctionalSetProperty<T>, IFunctionalGetProperty<T>
{
}

public class FunctionalProperty<T> : IFunctionalProperty<T>
{
    private Func<T> getter;
    private Action<T> setter;
    public T Value { get => getter.Invoke(); set => setter.Invoke(value); }

    public FunctionalProperty(Func<T> getter, Action<T> setter)
    {
        this.getter = getter;
        this.setter = setter;
    }
}

public class FunctionalGetter<T> : IFunctionalGetProperty<T>
{
    private Func<T> getter;
    public T Value => getter.Invoke();

    public FunctionalGetter(Func<T> getter)
    {
        this.getter = getter;
    }
}

public class FunctionalSetter<T> : IFunctionalSetProperty<T>
{
    private Action<T> setter;
    public T Value { set { setter.Invoke(value); } }

    public FunctionalSetter(Action<T> setter)
    {
        this.setter = setter;
    }
}
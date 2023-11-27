using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial;

public interface ILifespan
{
    bool IsDeadObject { get; }
    bool IsAliveObject { get => !IsDeadObject; }
}

public interface IReleasable
{
    internal void Release();
}

public interface IBinder
{
    T Bind<T>(T obj) where T : IReleasable;
}

internal interface IBinderLifespan : ILifespan, IBinder { }
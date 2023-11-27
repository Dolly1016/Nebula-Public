using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;
using Virial.Game;

namespace Virial.Assignable;

public abstract class AbstractRoleInstanceCommon : IBinder, ILifespan
{
    public Player MyPlayer { get; internal set; } = null!;
    
    internal IBinderLifespan MyBinderLifespan { get; set; } = null!;
    public bool IsDeadObject => MyBinderLifespan.IsDeadObject;
    public T Bind<T>(T obj) where T : IReleasable => MyBinderLifespan.Bind(obj);

    internal abstract void SetRole(AbstractRoleDef roleDef);

    public virtual void OnLocalActivated() { }
    public virtual void OnActivated() { }
    public virtual void OnLocalUpdate() { }
    public virtual void OnUpdate() { }
}

public abstract class AbstractRoleInstance<T> : AbstractRoleInstanceCommon where T : AbstractRoleDef
{
    protected internal T MyRole { get; internal set; } = null!;
    internal override void SetRole(AbstractRoleDef roleDef) => MyRole = (roleDef as T)!;
}
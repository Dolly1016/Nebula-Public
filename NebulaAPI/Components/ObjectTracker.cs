using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Virial.Components;

public interface ObjectTracker<T> : IGameEntity, IReleasable
{
    public T? CurrentTarget { get; }
    public bool IsLocked { get; set; }
}

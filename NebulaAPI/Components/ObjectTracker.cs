using Virial.Game;

namespace Virial.Components;

public interface ObjectTracker<T> : IGameOperator, IReleasable
{
    public T? CurrentTarget { get; }
    public bool IsLocked { get; set; }
}

using Virial.Game;

namespace Virial.Components;

public interface ObjectTracker<T> : IGameOperator, IReleasable
{
    public T? CurrentTarget { get; }
    public bool IsLocked { get; set; }
    internal void SetColor(UnityEngine.Color color);
    public void SetColor(Virial.Color color) => SetColor(color.ToUnityColor());
}

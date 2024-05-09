using Virial;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;


public abstract class INebulaScriptComponent : IGameOperator, ILifespan, IReleasable
{
    public INebulaScriptComponent()
    {
        this.Register(this);
    }

    void IReleasable.Release()
    {
        MarkedRelease = true;
    }

    public bool MarkedRelease { get; private set; } = false;

    bool ILifespan.IsDeadObject => MarkedRelease;
}

public class GameObjectBinding : INebulaScriptComponent, IGameOperator
{
    public GameObject? MyObject { get; private set; }

    public GameObjectBinding(GameObject binding) : base()
    {
        MyObject = binding;
    }

    public void Detach()
    {
        MyObject = null;
    }

    void IGameOperator.OnReleased() {
        if (MyObject) GameObject.Destroy(MyObject);
        MyObject = null;
    }
}

public class ComponentBinding<T> : INebulaScriptComponent, IGameOperator where T : MonoBehaviour 
{
    public T? MyObject { get; private set; }

    public ComponentBinding(T binding) : base()
    {
        MyObject = binding;
    }

    public void Detach()
    {
        MyObject = null;
    }
    void IGameOperator.OnReleased()
    {
        if (MyObject) GameObject.Destroy(MyObject!.gameObject);
    }
}

public class NebulaGameScript : INebulaScriptComponent, IGameOperator
{
    public Action? OnActivatedEvent = null;
    public Action? OnMeetingStartEvent = null;
    public Action? OnReleasedEvent = null;
    public Action? OnGameReenabledEvent = null;
    public Action? OnGameStartEvent = null;
    public Action? OnUpdateEvent = null;

    void IGameOperator.OnReleased() => OnReleasedEvent?.Invoke();
    void OnMeetingStart(MeetingStartEvent ev) => OnMeetingStartEvent?.Invoke();
    void OnGameReenabled(TaskPhaseRestartEvent ev) => OnGameReenabledEvent?.Invoke();
    void OnGameStart(GameStartEvent ev) => OnGameStartEvent?.Invoke();
    void Update(GameUpdateEvent ev)
    {
        if (OnActivatedEvent != null)
        {
            OnActivatedEvent.Invoke();
            OnActivatedEvent= null;
        }

        OnUpdateEvent?.Invoke();
    }
}
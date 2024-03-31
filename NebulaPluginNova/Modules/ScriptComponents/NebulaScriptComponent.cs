using Nebula.Events;
using Rewired.Data.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;


public abstract class INebulaScriptComponent : IGameEntity, ILifespan, IReleasable
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

public class GameObjectBinding : INebulaScriptComponent, IGameEntity
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

    void IGameEntity.OnReleased() {
        if (MyObject) GameObject.Destroy(MyObject);
        MyObject = null;
    }
}

public class ComponentBinding<T> : INebulaScriptComponent, IGameEntity where T : MonoBehaviour 
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
    void IGameEntity.OnReleased()
    {
        if (MyObject) GameObject.Destroy(MyObject!.gameObject);
    }
}

public class NebulaGameScript : INebulaScriptComponent, IGameEntity
{
    public Action? OnActivatedEvent = null;
    public Action? OnMeetingStartEvent = null;
    public Action? OnReleasedEvent = null;
    public Action? OnGameReenabledEvent = null;
    public Action? OnGameStartEvent = null;

    void IGameEntity.OnReleased() => OnReleasedEvent?.Invoke();
    void IGameEntity.OnMeetingStart() => OnMeetingStartEvent?.Invoke();
    void IGameEntity.OnGameReenabled() => OnGameReenabledEvent?.Invoke();
    void IGameEntity.OnGameStart() => OnGameStartEvent?.Invoke();
    void IGameEntity.Update()
    {
        if (OnActivatedEvent != null)
        {
            OnActivatedEvent.Invoke();
            OnActivatedEvent= null;
        }
    }
}
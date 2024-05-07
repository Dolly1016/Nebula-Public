using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

public interface IGameModule<T>
{
    T ModuleOwner { get; }
}

public abstract class AbstractGameModule<T> : IGameModule<T>
{
    private T owner;
    public T ModuleOwner => owner;

    public AbstractGameModule(T owner) {  this.owner = owner; }
}

public enum GameModuleInstantiationRule
{
    OnLobbyJoined,
    OnGameStarted
}

public interface IGameModuleHolder<T> {
    GameModule GetModule<GameModule>() where GameModule : IGameModule<T>;
}

public delegate IGameModule<T> GameModuleFactory<T>();

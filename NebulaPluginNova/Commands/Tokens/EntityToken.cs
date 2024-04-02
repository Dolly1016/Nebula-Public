using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Game;

namespace Nebula.Commands.Tokens;

/// <summary>
/// エンティティのトークンです
/// </summary>
public class EntityCommandToken : ICommandToken
{
    private IGameEntity entity;

    public EntityCommandToken(IGameEntity entity)
    {
        this.entity = entity;
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if (type == typeof(IGameEntity))
        {
            return new CoImmediateTask<T>(Unsafe.As<IGameEntity, T>(ref entity));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}


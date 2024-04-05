using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Game;

namespace Nebula.Commands.Tokens;

public class ObjectCommandToken<T> : ICommandToken
{
    private T obj;

    public ObjectCommandToken(T obj)
    {
        this.obj = obj;
    }

    CoTask<V> ICommandToken.AsValue<V>(CommandEnvironment env)
    {
        var myType = obj!.GetType();
        var type = typeof(V);

        if (myType.IsAssignableTo(type))
        {
            return new CoImmediateTask<V>(Unsafe.As<T, V>(ref obj));
        }

        return new CoImmediateErrorTask<V>();
    }
}

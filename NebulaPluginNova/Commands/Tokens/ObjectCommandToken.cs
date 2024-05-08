using System.Runtime.CompilerServices;
using Virial.Command;

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

        if (type == typeof(ICommandToken))
        {
            ICommandToken This = this;
            return new CoImmediateTask<V>(Unsafe.As<ICommandToken, V>(ref This));
        }
        else if (myType.IsAssignableTo(type))
        {
            return new CoImmediateTask<V>(Unsafe.As<T, V>(ref obj));
        }

        return new CoImmediateErrorTask<V>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;

namespace Nebula.Commands.Tokens;

public class BooleanCommandToken : ICommandToken
{
    private bool val;

    public BooleanCommandToken(bool val)
    {
        this.val = val;
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if (type == typeof(bool))
            return new CoImmediateTask<T>(Unsafe.As<bool, T>(ref val));
        else if (type == typeof(string))
        {
            string name = val ? "true" : "false";
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref name));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

public class IntegerCommandToken : ICommandToken
{
    private int val;

    public IntegerCommandToken(int val)
    {
        this.val = val;
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if (type == typeof(int))
            return new CoImmediateTask<T>(Unsafe.As<int, T>(ref val));
        else if (type == typeof(float))
        {
            float num = val;
            return new CoImmediateTask<T>(Unsafe.As<float, T>(ref num));
        }
        else if (type == typeof(string))
        {
            string name = val.ToString();
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref name));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

public class FloatCommandToken : ICommandToken
{
    private float val;

    public FloatCommandToken(float val)
    {
        this.val = val;
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if (type == typeof(float))
            return new CoImmediateTask<T>(Unsafe.As<float, T>(ref val));
        else if (type == typeof(int))
        {
            int num = (int)val;
            return new CoImmediateTask<T>(Unsafe.As<int, T>(ref num));
        }
        else if (type == typeof(string))
        {
            string name = val.ToString();
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref name));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

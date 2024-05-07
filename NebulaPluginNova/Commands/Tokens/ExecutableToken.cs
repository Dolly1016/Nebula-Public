using Il2CppSystem.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Game;

namespace Nebula.Commands.Tokens;

public class CommandExecutable : IExecutable
{
    ICommandToken inner;
    CommandEnvironment innerEnv;

    public CommandExecutable(ICommandToken inner, CommandEnvironment innerEnv)
    {
        this.inner = inner;
        this.innerEnv = innerEnv;
    }


    public CoTask<ICommandToken> CoExecute((string label, ICommandToken token)[] extra)
    {
        return inner.AsValue<ICommandToken>(innerEnv.SwitchArgumentTable(new LetsCommandModifier(extra, innerEnv.ArgumentTable)));
    }
}

public class WrappedExecutable : IExecutable
{
    Func<(string label,ICommandToken value)>[] arguments;
    IExecutable executable;

    public WrappedExecutable(IExecutable executable, Func<(string label, ICommandToken value)>[] arguments)
    {
        this.arguments = arguments;
        this.executable = executable;
    }

    public CoTask<ICommandToken> CoExecute((string label, ICommandToken token)[] extra)
    {
        return executable.CoExecute(this.arguments.Select(a => a.Invoke()).ToArray());
    }
}
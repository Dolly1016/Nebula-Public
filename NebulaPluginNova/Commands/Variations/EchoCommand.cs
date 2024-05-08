using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class EchoCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <value>");

        return arguments[0]
            .AsEnumerable(env).As<string>(env)
            .Do(str => env.Logger.Push(str));
    }
}

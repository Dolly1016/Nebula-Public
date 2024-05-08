using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class WaitCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <duration>");

        return arguments[0].AsValue<float>(env).Chain(val => new CoBuiltInTask<ICommandToken>(myTask => { myTask.Result = EmptyCommandToken.Token; return Effects.Wait(val).WrapToManaged(); }));
    }
}

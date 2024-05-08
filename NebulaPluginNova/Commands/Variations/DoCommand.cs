using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class DoCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(arguments).Select(a => a.AsEnumerable(env).Select(a => a.ToExecutable(env)?.CoExecute([]) ?? new CoImmediateErrorTask<ICommandToken>())).ChainFast(_ => EmptyCommandToken.Token);
    }
}

public class DoParallelCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(arguments).SelectParallel(
            a => a.AsEnumerable(env).DoParallel(a => a.ToExecutable(env)?.CoExecute([]) ?? new CoImmediateErrorTask<ICommandToken>()))
            .ChainFast(_ => EmptyCommandToken.Token);
            
    }
}
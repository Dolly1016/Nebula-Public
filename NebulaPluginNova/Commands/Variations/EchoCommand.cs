using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class EchoCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, ICommandModifier argumentTable, ICommandExecutor executor, ICommandLogger logger)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(logger, label + " <value>");

        return argumentTable.ApplyTo(arguments[0])
            .AsEnumerable(logger, executor, argumentTable).As<string>(logger, argumentTable, executor)
            .Do(str => logger.Push(str));
    }
}

using InnerNet;
using Nebula.Commands.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;
using Virial.Game;

namespace Nebula.Commands.Variations;

internal class RoomCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        return arguments[0].AsValue<string>(env).ChainFast(code => (ICommandToken)new IntegerCommandToken(GameCode.GameNameToInt(code)));
    }
}
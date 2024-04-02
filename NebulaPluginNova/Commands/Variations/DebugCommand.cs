using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;
public class DebugCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    class Test
    {
        public string? A = null;
        public int B;
        public GamePlayer? Player;
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 1)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <structure>");

        CommandStructureConverter<Test> test = new();
        test.Add<string>("a", (t,v) => t.A = v)
            .Add<int>("b", (t, v) => t.B = v)
            .Add<GamePlayer>("player", (t, v) => t.Player = v);
        return arguments[0].AsStructure(env).ConvertTo(test, new Test(), env).Action(t =>
        {
            Debug.Log("A = " + (t.A ?? "null"));
            Debug.Log("B = " + t.B);
            Debug.Log("Player = " + (t.Player?.Name ?? "null"));
        });
    }
}

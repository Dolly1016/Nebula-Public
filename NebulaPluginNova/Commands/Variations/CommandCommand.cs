using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;


public class CommandCommand : ICommand
{
    public class RuntimeCommand : ICommand
    {
        public RuntimeCommand(string[] arguments, IExecutable? executable) {
            this.executable = executable;
            this.arguments = arguments;
        }

        IExecutable? executable;
        string[] arguments;

        IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
        {
            return [];
        }
        CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
        {
            if(arguments.Count != this.arguments.Length)
                return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " command requires " + this.arguments.Length + " argument(s).");

            return new CoImmediateTask<IEnumerable<(string, ICommandToken)>>(Helpers.Sequential(arguments.Count).Select(i => (this.arguments[i], arguments[i])))
                .SelectParallel(val => val.Item2.EvaluateHere(env).ChainFast(evaluated => (val.Item1, evaluated)))
                .Chain(args => executable?.CoExecute(args.ToArray()) ?? new CoImmediateErrorTask<ICommandToken>(env.Logger, "This command is broken."));
        }
    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 2)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <arguments> <executable>");

        return arguments[0].AsValues<string>(env).ChainFast(args => (ICommandToken)new ObjectCommandToken<ICommand>(new RuntimeCommand(args.ToArray(), arguments[1].ToExecutable(env))));
    }
}
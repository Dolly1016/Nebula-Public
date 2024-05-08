using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class LetCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if(arguments.Count < 3)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <argument> <value> <expression>");
        
        return arguments[0].AsValue<string>(env)
            .Chain(argument =>
            {
                return arguments[1].EvaluateHere(env).Chain(val =>
                {
                    var letModifier = new LetCommandModifier(argument, val, env.ArgumentTable);
                    return CommandManager.CoExecute(arguments.Skip(2), env.SwitchArgumentTable(letModifier));
                });

                /*
                //引数の値を評価
                var value = env.ArgumentTable.ApplyTo(arguments[1]);

                //新たな環境を生成
                var letModifier = new LetCommandModifier(argument, value, env.ArgumentTable);

                //新たな環境下で中のコマンドを実行
                return CommandManager.CoExecute(arguments.Skip(2), env.SwitchArgumentTable(letModifier));
                */
            }, () => env.Logger.PushError("Uninterpretable variable name error."));
    }
}

public class LetsCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count % 2 == 0)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <argument1> <value1> ... <argumentN> <valueN> ( <expression> )");

        IEnumerator CoExecute(CoBuiltInTask<ICommandToken> myResult)
        {
            int i = 0;
            List<(string argument, ICommandToken token)> args = new();
            while (i + 1 < arguments.Count)
            {
                var task = arguments[i].AsValue<string>(env);
                yield return task.CoWait();
                if (task.IsFailed)
                {
                    env.Logger.PushError("Uninterpretable variable name error.");
                    myResult.IsFailed = true;
                    yield break;
                }

                var valTask = arguments[i + 1].EvaluateHere(env);
                yield return valTask.CoWait();
                if (valTask.IsFailed)
                {
                    env.Logger.PushError("The error occured during evaluating value.");
                    myResult.IsFailed = true;
                    yield break;
                }

                args.Add((task.Result, valTask.Result));

                i += 2;
            }

            var letModifier = new LetsCommandModifier(args, env.ArgumentTable);
            var letTask = arguments[arguments.Count - 1].ToExecutable(env.SwitchArgumentTable(letModifier))?.CoExecute([]) ?? new CoImmediateErrorTask<ICommandToken>();
            yield return letTask.CoWait();
            if (letTask.IsFailed)
            {
                myResult.IsFailed = true;
                yield break;
            }

            myResult.Result = letTask.Result;
        }
        return new CoBuiltInTask<ICommandToken>(CoExecute);
    }
}
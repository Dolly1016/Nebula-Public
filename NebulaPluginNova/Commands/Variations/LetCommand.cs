using Mono.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;

namespace Nebula.Commands.Variations;

public class LetCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, ICommandModifier argumentTable, ICommandExecutor executor, ICommandLogger logger)
    {
        if(arguments.Count < 3)
            return new CoImmediateErrorTask<ICommandToken>(logger, label + " <argument> <value> <expression>");
        
        return arguments[0].AsValue<string>(logger, executor, argumentTable)
            .Chain(argument =>
            {
                //引数の値を評価
                var value = argumentTable.ApplyTo(arguments[1]);

                //新たな環境を生成
                var letModifier = new LetCommandModifier(argument, value, argumentTable);

                //新たな環境下で中のコマンドを実行
                return CommandManager.CoExecute(arguments.Skip(2), letModifier, executor, logger);
            }, () => logger.PushError("Uninterpretable variable name error."));
    }
}

public class LetsCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, ICommandModifier argumentTable, ICommandExecutor executor, ICommandLogger logger)
    {
        if (arguments.Count % 2 == 0)
            return new CoImmediateErrorTask<ICommandToken>(logger, label + " <argument1> <value1> ... <argumentN> <valueN> ( <expression> )");

        IEnumerator CoExecute(CoBuiltInTask<ICommandToken> myResult)
        {
            int i = 0;
            List<(string argument, ICommandToken token)> args = new();
            while (i + 1 < arguments.Count)
            {
                var task = arguments[i].AsValue<string>(logger, executor, argumentTable);
                yield return task.CoWait();
                if (task.IsFailed)
                {
                    logger.PushError("Uninterpretable variable name error.");
                    myResult.IsFailed = true;
                    yield break;
                }
                args.Add((task.Result, argumentTable.ApplyTo(arguments[i + 1])));

                i += 2;
            }

            var letModifier = new LetsCommandModifier(args, argumentTable);
            var letTask = arguments[arguments.Count - 1].EvaluateHere(logger, executor, letModifier);
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
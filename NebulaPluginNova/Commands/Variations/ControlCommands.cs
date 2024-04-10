using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class IfCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count == 1) return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " (<condition> <elseIfTrue>)+ [<ifFalse>]");

        //さいごの偽の場合のトークンは切り捨てられる
        int num = arguments.Count / 2;
        bool hasFalse = (arguments.Count % 2) == 1;

        IEnumerator CoEvaluate(CoBuiltInTask<ICommandToken> myTask)
        {
            for(int i =  0; i < num; i++)
            {
                var ifTask = arguments[i * 2].AsValue<bool>(env);
                yield return ifTask.CoWait();
                if (ifTask.IsFailed)
                {
                    myTask.IsFailed = true; 
                    yield break;
                }

                if (ifTask.Result)
                {
                    var resultTask = arguments[i * 2 + 1].EvaluateHere(env);
                    yield return resultTask.CoWait();
                    if (resultTask.IsFailed)
                        myTask.IsFailed = true;
                    else
                        myTask.Result = resultTask.Result;
                    yield break;
                }
            }

            //全部偽だった場合
            if (hasFalse)
            {
                var resultTask = arguments[arguments.Count - 1].EvaluateHere(env);
                yield return resultTask.CoWait();
                if (resultTask.IsFailed)
                    myTask.IsFailed = true;
                else
                    myTask.Result = resultTask.Result;
            }
        }

        return new CoBuiltInTask<ICommandToken>(CoEvaluate);
    }
}
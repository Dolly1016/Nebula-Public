using Il2CppSystem.Runtime.Remoting.Messaging;
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
        if (arguments.Count == 1) return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <condition1> <then1> <condition2> <then2> ... [<else>]");

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

public class AtCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count != 2) return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <collection> <index>");


        return arguments[0].AsStructure(env)
            .Chain(structure => arguments[1].AsValue<string>(env).Chain(label => (CoTask<ICommandToken>)(structure.TryGetValue(label, out var val) ? new CoImmediateTask<ICommandToken>(val) : new CoImmediateErrorTask<ICommandToken>(env.Logger, "Non-existent label is specified.")))
            , () => arguments[1].AsValue<int>(env).Chain(num => arguments[0].AsEnumerable(env).Chain(col =>
            {
                var array = col.ToArray();
                if (num < 0) num = array.Length - num;
                if (array.Length <= num || num < 0)
                    return (CoTask<ICommandToken>)new CoImmediateErrorTask<ICommandToken>(env.Logger, $"Given index is out of range! (Length: {array.Length}, Index: {num})");
                return (CoTask<ICommandToken>)new CoImmediateTask<ICommandToken>(array[num]);
            })));
    }
}
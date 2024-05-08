using Virial.Command;
using Virial.Compat;
using Virial.Helpers;

namespace Nebula.Commands.Variations;

public class ColCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<IEnumerable<ICommandToken>> WhereComponent(IEnumerable<ICommandToken> token, ICommandToken func, string variable, CommandEnvironment env)
    {
        IEnumerator CoEvaluate(CoBuiltInTask<IEnumerable<ICommandToken>> myTask)
        {
            var evaluators = token.Select(t => (t, func.AsValue<bool>(env.SwitchArgumentTable(new LetCommandModifier(variable, t, env.ArgumentTable))))).ToArray();
            yield return evaluators.Select(e => e.Item2.CoWait().HighSpeedEnumerator()).WaitAll();
            myTask.Result = evaluators.Where(e => !e.Item2.IsFailed && e.Item2.Result).Select(e => e.t);
        }
        return new CoBuiltInTask<IEnumerable<ICommandToken>>(CoEvaluate);
    }

    CoTask<IEnumerable<ICommandToken>> SelectComponent(IEnumerable<ICommandToken> token, ICommandToken func, string variable, CommandEnvironment env)
    {
        IEnumerator CoEvaluate(CoBuiltInTask<IEnumerable<ICommandToken>> myTask)
        {
            var evaluators = token.Select(t => func.EvaluateHere(env.SwitchArgumentTable(new LetCommandModifier(variable, t, env.ArgumentTable)))).ToArray();
            yield return evaluators.Select(e => e.CoWait().HighSpeedEnumerator()).WaitAll();
            myTask.Result = evaluators.Where(e => !e.IsFailed).Select(e => e.Result);
        }
        return new CoBuiltInTask<IEnumerable<ICommandToken>>(CoEvaluate);
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (arguments.Count % 2 != 0 || arguments.Count == 0)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <collection> <variable> <effectors>");

        string p = "-";
        var task = arguments[1].AsValue<string>(env).Action(val => p = val).Chain(_ => arguments[0].AsEnumerable(env));
        int num = arguments.Count / 2 - 1;



        for (int i = 0; i < num; i++)
        {
            int copiedIndex = i;
            task = task.Chain(col =>
            {
                int n = (copiedIndex + 1) * 2;
                return arguments[n].AsValue<string>(env).ChainIf<IEnumerable<ICommandToken>, string>(new()
                {
                    { "where", () => WhereComponent(col, arguments[n + 1], p, env)},
                    { "filter",  () =>WhereComponent(col, arguments[n + 1], p, env)},
                    { "select", () => SelectComponent(col, arguments[n + 1], p, env)},
                    { "map",  () =>SelectComponent(col, arguments[n + 1], p, env)},
                });
            });
        }

        return task.ChainFast<ICommandToken, IEnumerable<ICommandToken>>(col =>
        {
            return new ArrayCommandToken(new ReadOnlyArray<ICommandToken>(col));
        });
    }
}

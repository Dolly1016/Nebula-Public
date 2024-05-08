using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class RandomCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }
    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if(arguments.Count == 1)
        {
            return arguments[0].AsEnumerable(env).ChainFast(val =>
            {
                var array = val.ToArray();
                return array.Length > 0 ? array.Random() : EmptyCommandToken.Token;
            });
        }

        if (arguments.Count != 3)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " integer|float <min (included)> <max (not included)>");

        bool isFloat = false;
        float min = 0, max = 0;
        return arguments[0].AsValue<string>(env).Action(val => isFloat = val == "float")
            .Chain(_ => arguments[1].AsValue<float>(env)).Action(val => min = val)
            .Chain(_ => arguments[2].AsValue<float>(env)).Action(val => max = val)
            .ChainFast<ICommandToken, ICommandToken>(_ =>
            {
                if (isFloat)
                    return new FloatCommandToken((float)System.Random.Shared.NextDouble() * (max - min) + min);
                else
                    return new IntegerCommandToken(System.Random.Shared.Next((int)min,(int)max));
            });
    }
}

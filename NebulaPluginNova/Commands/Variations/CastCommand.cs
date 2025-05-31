using Nebula.Commands.Tokens;
using Virial.Assignable;
using Virial.Command;
using Virial.Compat;
using Virial.Game;

namespace Nebula.Commands.Variations;

public class CastCommand : ICommand
{
    private static Dictionary<string, Func<ICommandToken, CommandEnvironment, CoTask<ICommandToken>>> objectDics = new()
    {
        {  "player", GetCastTask<GamePlayer> },
        {  "outfit", GetCastTask<OutfitDefinition> },
    };

    public static CoTask<ICommandToken> GetCastTask<T>(ICommandToken argument, CommandEnvironment env)
    {
        return argument.AsValue<T>(env).ChainFast(val => (ICommandToken)new ObjectCommandToken<T>(val));
    }


    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if(arguments.Count != 2)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <type> <value>");

        return arguments[0].AsValue<string>(env).Chain(type => {
            return type.ToLower() switch
            {
                "string" => arguments[1].AsValue<string>(env).ChainFast(v => (ICommandToken) new StringCommandToken(v)),
                "integer" => arguments[1].AsValue<int>(env).ChainFast(v => (ICommandToken) new IntegerCommandToken(v)),
                "float" => arguments[1].AsValue<float>(env).ChainFast(v => (ICommandToken) new FloatCommandToken(v)),
                "boolean" => arguments[1].AsValue<bool>(env).ChainFast(v => (ICommandToken) new BooleanCommandToken(v)),
                "player" => arguments[1].AsValue<GamePlayer>(env).ChainFast(v => (ICommandToken)new PlayerCommandToken(v)),
                "role" => arguments[1].AsValue<DefinedRole>(env).ChainFast(v => (ICommandToken)new RoleCommandToken(v)),
                var t => objectDics.TryGetValue(t,out var func) ? func.Invoke(arguments[1], env) : new CoImmediateErrorTask<ICommandToken>(env.Logger, $"Invalid type name \"{t}\" is received.")
            };
        });
    }
}

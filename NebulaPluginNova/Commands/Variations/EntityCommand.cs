using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;
using Virial.Game;

namespace Nebula.Commands.Variations;


public class EntityCommand : ICommand
{
    public class EntityCommandDefinition
    {
        public CoTaskChainer<ICommandToken, CommandStructure> AddChainer { get; private init; }
        
        public EntityCommandDefinition(CoTaskChainer<ICommandToken, CommandStructure> addChainer)
        {
            AddChainer = addChainer;
        }
    }

    static public EntityCommandDefinition GenerateDefinition<T>(CommandStructureConverter<T> converter, Func<T> constructor, Func<T, IGameOperator> entityGenerater)
    {
        return new(
            (task, env) =>
            {
                return converter.ChainConverterTo(task, constructor.Invoke(), env).ChainFast<ICommandToken, T>(t => new ObjectCommandToken<IGameOperator>(entityGenerater.Invoke(t)));
            }
            );
    }

    private static Dictionary<string, EntityCommandDefinition> entityDefinitions = new();
    public static void RegisterEntityDefinition(string id, EntityCommandDefinition definition)
    {
        entityDefinitions[id] = definition;
    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count < 2)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " add|remove ...");

        return arguments[0].AsValue<string>(env)
            .ChainIf<ICommandToken, string>(new(){
                { "add", () => {
                    return arguments[1].AsValue<string>(env).Chain(id =>
                    {
                        if(entityDefinitions.TryGetValue(id, out var def))
                        {
                            return def.AddChainer.Invoke(arguments.Count == 3 ? arguments[2].AsStructure(env) : new CoImmediateTask<CommandStructure>(new CommandStructure()), env);
                        }
                        else
                            return new CoImmediateErrorTask<ICommandToken>(env.Logger, $"unknown entity-id \"{ id }\"");
                        
                    });
                }} ,
                { "remove", () => new CoImmediateErrorTask<ICommandToken>() }
            });
    }
}
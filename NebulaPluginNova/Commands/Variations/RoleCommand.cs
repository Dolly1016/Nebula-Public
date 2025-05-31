using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class RoleCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count == 0)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <player> [<role> [<arguments>]]");

        IEnumerable<GamePlayer> players = null!;
        CoTask<ICommandToken> task = arguments[0].AsEnumerable(env).AsParallel<GamePlayer>(env).Action(p => players = p);
        
        if(arguments.Count == 1)
        {
            return task.Chain(_ =>
            {
                var role = players.FirstOrDefault()?.Role.Role;
                if (role == null) return new CoImmediateErrorTask<ICommandToken>();
                return (CoTask<ICommandToken>) new CoImmediateTask<ICommandToken>(new RoleCommandToken(role));
            });
        }
        else
        {
            int[] roleArgs = new int[0];
            if (arguments.Count == 3)
                task = task.Chain(_ => arguments[2].AsEnumerable(env).As<int>(env).Action(vals => roleArgs = vals.ToArray()));
                
            return task.Chain(_ => arguments[1].AsValue<Virial.Assignable.DefinedRole>(env).Action(role => {
                using (RPCRouter.CreateSection("RoleCommand"))
                {
                    foreach (var p in players) PlayerModInfo.RpcSetAssignable.Invoke((p.PlayerId, role.Id, roleArgs, RoleType.Role));
                }
            }));
        }
    }
}

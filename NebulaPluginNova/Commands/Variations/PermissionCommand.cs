using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Common;
using Virial.Compat;


namespace Nebula.Commands.Variations;

internal class PermissionCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByOpPermission(env, out var p)) return p;

        if (arguments.Count != 3)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <player> add|remove|test <permission>");

        GamePlayer player = null!;
        string method = null!;
        string permissionName = null!;
        Permission permission = null!;
        bool inverse = false;
        CoTask<ICommandToken> task = arguments[0].AsValue<GamePlayer>(env).Action(p => player = p);
        task = task.Chain(_ => arguments[1].AsValue<string>(env).Action(val => method = val));
        task = task.Chain(_ => arguments[2].AsValue<string>(env).Action(val =>
        {
            if (val.StartsWith("~"))
            {
                inverse = true;
                val = val[1..];
            }
            permissionName = val;
            Permissions.TryGetPermission(val, out permission!);
        }));
        return task.Action(_ =>
        {
            if(permission == null)
            {
                env.Logger.Push("Unknown permission.");
                return;
            }
            switch (method)
            {
                case "add":
                    PlayerModInfo.RpcEditPermission.Invoke((player, permissionName, inverse, true));
                    break;
                case "remove":
                    PlayerModInfo.RpcEditPermission.Invoke((player, permissionName, inverse, false));
                    break;
                case "test":
                    env.Logger.Push(player.Test(permission) ? (player.PlayerName + " has this permission.") : player.PlayerName + " doesn't have this permission.");
                    break;
                default:
                    env.Logger.Push(method + " is invalid method.");
                    break;
            }
        });
    }
}

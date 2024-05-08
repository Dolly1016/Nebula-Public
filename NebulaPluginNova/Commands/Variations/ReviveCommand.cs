using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

public class ReviveCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count == 0)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <player>");

        IEnumerable<GamePlayer> players = null!;
        GamePlayer? healer = null;

        return arguments[0]
            .AsEnumerable(env)
            .AsParallel<GamePlayer>(env)
            .Action(p => players = p)
            .Chain(_ =>
                NebulaCommandHelper.InterpretClause(arguments.Skip(1), [
                    new CommandClause("by", 1, args => args[0].AsValues<GamePlayer>(env).Do(p => healer = p).Discard<bool, ICommandToken>())
                    ], env)
                    .Action(_ => {
                        int count = 0;
                        using (RPCRouter.CreateSection("reviveCommand"))
                        {

                            foreach (var player in players)
                            {
                                if (!player.IsDead) continue;

                                player.VanillaPlayer.ModRevive((healer ?? player).VanillaPlayer, player.VanillaPlayer.transform.position, true);
                                count++;
                            }
                        }
                        env.Logger.Push("revived " + count + " player(s)");
                    }));
    }
}

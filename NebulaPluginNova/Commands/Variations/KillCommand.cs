using Nebula.Game.Statistics;
using Virial.Command;
using Virial.Compat;
using Virial.Game;

namespace Nebula.Commands.Variations;

public class KillCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count == 0)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <player> [options...]");

        IEnumerable<GamePlayer> players = null!;
        bool leftDeadBody = !MeetingHud.Instance;
        bool evenIfDead = false;
        bool blink = false;
        GamePlayer? killer = null;

        return arguments[0]
            .AsEnumerable(env)
            .AsParallel<GamePlayer>(env)
            .Action(p => players = p)
            .Chain(_ =>
                NebulaCommandHelper.InterpretClause(arguments.Skip(1), [
                    new CommandClause("withCorpse", 0, args => { leftDeadBody = true; return new CoImmediateTask<bool>(true); }),
                    new CommandClause("noCorpse", 0, args => { leftDeadBody = false; return new CoImmediateTask<bool>(true); }),
                    new CommandClause("evenIfDead", 0, args => { evenIfDead = true; return new CoImmediateTask<bool>(true); }),
                    new CommandClause("blink", 0, args => { blink = true; return new CoImmediateTask<bool>(true); }),
                    new CommandClause("by", 1, args => args[0].AsEnumerable(env).As<GamePlayer>(env).Do(p => killer = p).Discard<bool, ICommandToken>())
                    ], env)
                    .Action(_ => {
                        int count = 0;
                        using (RPCRouter.CreateSection("killCommand"))
                        {
                            KillParameter param = KillParameter.WithOverlay | KillParameter.WithAssigningGhostRole;
                            if (leftDeadBody) param |= KillParameter.WithDeadBody;
                            if (blink) param |= KillParameter.WithBlink;
                            if (MeetingHud.Instance) param |= KillParameter.WithKillSEWidely;

                            foreach (var player in players)
                            {
                                if (player.IsDead && !evenIfDead) continue;

                                (killer ?? player).MurderPlayer(player, PlayerState.Dead, EventDetail.Kill, param, KillCondition.NoCondition);

                                count++;
                            }
                        }
                        env.Logger.Push("killed " + count + " player(s)");
                    }));
    }
}

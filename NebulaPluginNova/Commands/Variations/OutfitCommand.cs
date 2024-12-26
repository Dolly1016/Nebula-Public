using Nebula.Commands.Tokens;
using Virial.Command;
using Virial.Compat;
using Virial.Game;

namespace Nebula.Commands.Variations;

public class OutfitCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count < 2)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " get|set <target> ...");

        IEnumerable<GamePlayer> targets = [];
        int priority = 50;
        bool selfAware = true;
        bool isSet = false;
        return arguments[0].AsValue<string>(env).Action(val => isSet = val == "set")
            .Chain(_ => arguments[1].AsEnumerable(env).As<GamePlayer>(env).Action(p => targets = p))
            .Chain(_ =>
            {
                CoTask<ICommandToken> task = new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token);

                if (isSet)
                {
                    if (arguments.Count < 3)
                        return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " set <target> <outfit> [<priority>] [<selfAware>]");

                    OutfitDefinition? outfit = null;
                    task = task.Chain(_ => arguments[2].AsValue<OutfitDefinition>(env).Action(val => outfit = val));
                    if (arguments.Count >= 4) task = task.Chain(_ => arguments[3].AsValue<int>(env).Action(val => priority = val));
                    if (arguments.Count == 5) task = task.Chain(_ => arguments[4].AsValue<bool>(env).Action(val => selfAware = val));

                    return task.Action(_ =>
                    {
                        if (outfit == null)
                            env.Logger.PushError("The given outfit is invalid.");
                        else
                        {
                            using (RPCRouter.CreateSection("CommandOutfit"))
                            {
                                targets.Do(p => PlayerModInfo.RpcAddOutfit.Invoke((p.PlayerId, new(outfit, "", priority, selfAware))));
                            }
                        }
                    });
                }
                else
                {
                    if (arguments.Count == 3) task = task.Chain(_ => arguments[2].AsValue<int>(env).Action(val => priority = val));

                    return task.ChainFast(_ => (targets.Count() == 0 ? EmptyCommandToken.Token
                    : new ObjectCommandToken<OutfitDefinition>(targets.First().Unbox().GetOutfit(priority).Outfit)));
                }
            });
    }
}


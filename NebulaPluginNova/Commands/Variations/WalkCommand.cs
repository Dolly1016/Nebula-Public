using Nebula.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;
using Virial.Utilities;

namespace Nebula.Commands.Variations;

internal class WalkCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count != 2)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <bot> <player>");

        GamePlayer? bot = null;
        GamePlayer? player = null;
        return arguments[0].AsValues<GamePlayer>(env)
            .Do(p => bot = p)
            .Chain(_ => arguments[1].AsValues<GamePlayer>(env))
            .Do(p =>
            {
                var player = p;

                if (bot == null) {
                    env.Logger.Push("No bot selected.");
                    return;
                }
                if (player == null) {
                    env.Logger.Push("No target player selected.");
                    return;
                }

                if (!bot.VanillaPlayer.isDummy)
                {
                    env.Logger.Push("Only dummies can be moved!");
                    return;
                }

                UnityEngine.Vector2[]? path = null;

                path = NavVerticesHelpers.CalcPath(bot.TruePosition, player.TruePosition);
                if(path == null)
                {
                    env.Logger.Push("The player cannot navigate to this location.");
                    return;
                }
                NebulaManager.Instance.StartCoroutine(NavVerticesHelpers.WalkPath(path, new VanillaPlayerLogics(bot.VanillaPlayer, bot)).WrapToIl2Cpp());
                env.Logger.Push("Start walking.");
            });
    }
}

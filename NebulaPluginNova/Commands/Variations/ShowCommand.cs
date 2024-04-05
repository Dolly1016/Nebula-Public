using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;
using Virial.Media;

namespace Nebula.Commands.Variations;

public class ShowCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count < 2)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " gui <options...>");

        return arguments[0].AsValue<string>(env)
            .ChainIf<ICommandToken, string>(new(){
                { "gui", () => {
                    if(!(arguments.Count is 4 or 6)) return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " gui <width> <height> <guiContent>");
                    float width = 3f, height = 2.5f;
                    float pivotX = 0f, pivotY = 1f;
                    CoTask<ICommandToken> task =
                        arguments[1].AsValue<float>(env).Action(val => width = val)
                        .Chain(_ => arguments[2].AsValue<float>(env)).Action(val => height = val);
                    if(arguments.Count == 6)
                        task = task
                        .Chain(_ => arguments[3].AsValue<float>(env)).Action(val => pivotX = Mathf.Clamp01(val))
                        .Chain(_ => arguments[4].AsValue<float>(env)).Action(val => pivotY = Mathf.Clamp01(val));

                    return task.Chain(_ => arguments[arguments.Count - 1].AsValue<GUIWidget>(env))
                    .Action(widget =>
                    {
                        var window = MetaScreen.GenerateWindow(new(width, height),  HudManager.Instance.transform, UnityEngine.Vector3.zero, true, true, true, true);
                        window.SetWidget(widget, new(pivotX, pivotY), out _);
                    });
                }}
            });
    }
}


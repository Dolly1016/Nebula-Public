using Virial;
using Virial.Command;
using Virial.Compat;

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
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " gui|title <options...>");

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
                        var window = MetaScreen.GenerateWindow(new(width, height),  HudManager.Instance.transform, UnityEngine.Vector3.zero, true, true, withMask: true);
                        window.SetWidget(widget, new UnityEngine.Vector2(pivotX, pivotY), out _);
                    });
                }},
                { "title", () =>
                {
                    if(!(arguments.Count is 3 or 4 or 6 or 7)) return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " title <duration> <text>");
                    float duration = 10f;
                    Color color = Color.white;
                    
                    CoTask<ICommandToken> task =
                        arguments[1].AsValue<float>(env).Action(val => duration = val);
                    if(arguments.Count >= 6){
                        int r = 255,g = 255,b = 255;
                        task = task
                        .Chain(_ => arguments[2].AsValue<int>(env)).Action(val => r = Mathf.Clamp(val,0,255))
                        .Chain(_ => arguments[3].AsValue<int>(env)).Action(val => g = Mathf.Clamp(val,0,255))
                        .Chain(_ => arguments[4].AsValue<int>(env)).Action(val => b = Mathf.Clamp(val,0,255))
                        .Action(_ => color = new Color(r/255f,g/255f,b/255f,1f));
                    }
                    string subString = "";
                    int diff = 0;
                    if(arguments.Count % 3 == 1){
                        diff = 1;
                        task = task.Chain(_ => arguments[arguments.Count - 1].AsValue<string>(env)).Action(val => subString = val);
                    }

                    return task.Chain(_ => arguments[arguments.Count - 1 - diff].AsValue<string>(env))
                    .Action(text =>
                    {
                        if(subString.Length > 0) text += "<br><size=40%>" + subString + "</size>";
                        NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetPivot(new(0.5f,0.5f)).SetText(text, color, duration);
                    });
                }}
            });
    }
}


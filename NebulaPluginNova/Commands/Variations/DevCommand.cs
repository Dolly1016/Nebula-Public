using Nebula.Map;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Variations;

internal class DevCommand : ICommand
{
    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        //if (CommandHelper.DenyByOpPermission(env, out var p)) return p;

        if(arguments.Count == 0) return new CoImmediateErrorTask<ICommandToken>(env.Logger, "1 or more arguments are required.");

        return arguments.Select(a => a.AsValue<string>(env)).SelectAsCoTask().Chain(parsed =>
        {
            CoTask<ICommandToken>? result = null;
            switch (parsed[0].ToLower().Replace("_", ""))
            {
                case "displaydot":
                    result = DisplayDotCommand(parsed);
                    break;

            }
            return result ?? new CoImmediateErrorTask<ICommandToken>(env.Logger, "Bad argument error.");
        });
    }

    CoTask<ICommandToken>? DisplayDotCommand(IReadOnlyList<string> parsedArgs)
    {
        string kind = parsedArgs.Count > 1 ? parsedArgs[1].ToLower() : "maparea";
        Color color = parsedArgs.Count > 2 ? (ColorUtility.TryParseHtmlString(parsedArgs[2], out var c) ? c : Color.white) : Color.white;
        IEnumerable<(UnityEngine.Vector2 point, string text)>? points = null;
        switch (kind)
        {
            case "maparea":
                points = MapData.GetCurrentMapData().MapArea.Select(p => (p, ""));
                break;
            case "nonmaparea":
                points = MapData.GetCurrentMapData().NonMapArea.Select(p => (p, ""));
                break;
            case "objpos":
            case "objects":
            case "objectpos":
            case "mapobjectpoints":
                points = MapData.GetCurrentMapData().MapObjectPoints.Select(p => (p.Point.ToUnityVector(), ""));
                break;
        }
        if(points == null) return new CoImmediateErrorTask<ICommandToken>(null, $"Unknown displaydot option. \"{kind}\"");
        return new CoActionTask(() => points.Do(p => Helpers.DisplayDot(p.point, p.text, color)));
    }
}


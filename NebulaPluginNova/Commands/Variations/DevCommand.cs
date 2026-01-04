using Il2CppSystem.Net;
using Nebula.AeroGuesser;
using Nebula.Map;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Command;
using Virial.Compat;
using Virial.Events.Game;

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
                    result = DisplayDotCommand(false, parsed, env);
                    break;
                case "displayarea":
                    result = DisplayDotCommand(true, parsed, env);
                    break;
                case "here":
                    result = new CoActionTask(()=>
                    {
                        var text = env.Logger.Push("");
                        GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => {
                            var pos = GamePlayer.LocalPlayer?.Position ?? new(0f, 0f);
                            text.UpdateText($"({pos.x:F2}, {pos.y:F2})");
                        }, NebulaAPI.CurrentGame!);
                    });
                    break;
            }
            return result ?? new CoImmediateErrorTask<ICommandToken>(env.Logger, "Bad argument error.");
        });
    }

    private SpriteRenderer lastTempBox = null!;
    CoTask<ICommandToken>? DisplayDotCommand(bool displayArea, IReadOnlyList<string> parsedArgs, CommandEnvironment env)
    {
        string kind = parsedArgs.Count > 1 ? parsedArgs[1].ToLower() : "maparea";
        Color color = parsedArgs.Count > 2 ? (ColorUtility.TryParseHtmlString(parsedArgs[2], out var c) ? c : Color.white) : Color.white;
        IEnumerable<(UnityEngine.Vector2 point, string text, UnityEngine.Vector2 size)>? points = null;
        switch (kind)
        {
            case "maparea":
                points = MapData.GetCurrentMapData().MapArea.Select(p => (p, "", UnityEngine.Vector2.zero));
                break;
            case "nonmaparea":
                points = MapData.GetCurrentMapData().NonMapArea.Select(p => (p, "", UnityEngine.Vector2.zero));
                break;
            case "objpos":
            case "objects":
            case "objectpos":
            case "mapobjectpoints":
                points = MapData.GetCurrentMapData().MapObjectPoints.Select(p => (p.Point.ToUnityVector(), "", UnityEngine.Vector2.zero));
                break;
            case "aeroguessereasy":
            case "aeroeasy":
            case "aeroguessernormal":
            case "aeronormal":
                points = AeroGuesserQuizData.GetAllEntry(AmongUsUtil.CurrentMapId, 0).Select(p => (p.position, p.comment, p.viewport));
                break;
            case "aeroguesserhard":
            case "aerohard":
                points = AeroGuesserQuizData.GetAllEntry(AmongUsUtil.CurrentMapId, 1).Select(p => (p.position, p.comment, p.viewport));
                break;
            case "opportunist":
                points = Roles.Neutral.Opportunist.GetTaskPositions().Select(p => (p.center, "", p.size));
                break;
            case "temp":
                if (parsedArgs.Count < 7) return new CoImmediateErrorTask<ICommandToken>(null, $"7 arguments required for \"{kind}\" option in displaydot command.");
                float posX = 0f, posY = 0f, areaX = 0f, areaY = 0f;
                bool success =
                        float.TryParse(parsedArgs[3], out posX) &&
                        float.TryParse(parsedArgs[4], out posY) &&
                        float.TryParse(parsedArgs[5], out areaX) &&
                        float.TryParse(parsedArgs[6], out areaY);
                if (!success) return new CoImmediateErrorTask<ICommandToken>(null, $"Invalid arguments.");
                return new CoActionTask(() =>
                {
                    if (lastTempBox) GameObject.Destroy(lastTempBox.gameObject);
                    lastTempBox = Helpers.DisplayArea(new(posX, posY), color, new(areaX, areaY));
                    if(parsedArgs.Count == 8 && parsedArgs[7] == "copy")
                    {
                        ClipboardHelper.PutClipboardString($"(new({posX:F1}f, {posY:F1}f), new({areaX:F1}f, {areaY:F1}f), \"\"),");
                        env.Logger.Push("Copied the entry tuple.");
                    }
                });

        }
        if(points == null) return new CoImmediateErrorTask<ICommandToken>(null, $"Unknown displaydot option. \"{kind}\"");

        if (displayArea)
        {
            return new CoActionTask(() =>
            {
                int num = 0;
                points.Do(p =>
                {
                    Helpers.DisplayArea(p.point, color, p.size);
                    num++;
                });
                env.Logger.Push(num + " area(s) has been displayed.");
            });
        }
        else
        {
            return new CoActionTask(() =>
            {
                int num = 0;
                points.Do(p =>
                {
                    Helpers.DisplayDot(p.point, p.text, color, p.size);
                    num++;
                });
                env.Logger.Push(num + " dot(s) has been displayed.");
            });
        }
    }
}


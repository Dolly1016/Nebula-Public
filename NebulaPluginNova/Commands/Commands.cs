using Innersloth.IO;
using Microsoft.VisualBasic;
using Mono.CSharp;
using Nebula.Commands.Variations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Commands;

[NebulaPreLoad(typeof(NebulaResourceManager))]
static public class Commands
{
    static public void Load()
    {
        CommandManager.RegisterCommand(new FormulaCommand(), "nebula::formula", "nebula::f");
        CommandManager.RegisterCommand(new IfCommand(), "nebula::if");
        CommandManager.RegisterCommand(new LetCommand(), "nebula::let");
        CommandManager.RegisterCommand(new LetsCommand(), "nebula::lets", "nebula::scope");
        CommandManager.RegisterCommand(new EchoCommand(), "nebula::echo");
        CommandManager.RegisterCommand(new KillCommand(), "nebula::kill");
        CommandManager.RegisterCommand(new ReviveCommand(), "nebula::revive");
        CommandManager.RegisterCommand(new EntityCommand(), "nebula::entity");
        CommandManager.RegisterCommand(new OutfitCommand(), "nebula::outfit");
        CommandManager.RegisterCommand(new ColCommand(), "nebula::col");
        CommandManager.RegisterCommand(new DoCommand(), "nebula::do");
        CommandManager.RegisterCommand(new DoParallelCommand(), "nebula::parallel");
        CommandManager.RegisterCommand(new WaitCommand(), "nebula::wait");
        CommandManager.RegisterCommand(new RandomCommand(), "nebula::random");
        CommandManager.RegisterCommand(new ShowCommand(), "nebula::show");
        CommandManager.RegisterCommand(new EffectCommand(), "nebula::effect");
        CommandManager.RegisterCommand(new GuiHolderCommand(), "gui::holder");
        CommandManager.RegisterCommand(new GuiTextCommand(), "gui::text");
        CommandManager.RegisterCommand(new GuiButtonCommand(), "gui::button");
        CommandManager.RegisterCommand(new GuiArrayerCommand(), "gui::arrayer");

        /*
        CommandManager.RegisterCommand(new Command((bubble, args, result) =>
        {
            IEnumerator CoExecute()
            {
                if (args.Length != 1)
                {
                    bubble.PushResponse("echo <object>".Color(invalidColor));
                    yield break;
                }

                yield return args[0].Evaluate(bubble);
                result.Value = args[0];

                foreach (var a in args[0].GetStringEnumerator(bubble)) bubble.PushResponse(a);

                yield break;
            }
            return CoExecute();
        })
        , "echo");

        CommandManager.RegisterCommand(new Command((bubble, args, result) =>
        {
            IEnumerator CoExecute()
            {
                if (args.Length != 1)
                {
                    bubble.PushResponse("player <playerName>".Color(invalidColor));
                    yield break;
                }

                var name = args[0].GetString(bubble);
                var playerId = PlayerControl.AllPlayerControls.Find((Il2CppSystem.Predicate<PlayerControl>)(p => p.name == name))?.PlayerId;
                result.Value = new StaticCommandArgument(playerId?.ToString() ?? "255");
                yield break;
            }
            return CoExecute();
        })
        , "player");

        CommandManager.RegisterCommand(new Command((bubble, args, result) =>
        {
            IEnumerator CoExecute()
            {
                if (args.Length < 2)
                {
                    bubble.PushResponse("property <property> [as integer|string] [on <playerId>]".Color(invalidColor));
                    yield break;
                }
                var property = args[0].GetString(bubble);
                IEnumerable<int>? player = null;
                Type? propertyType = null;

                int index = 1;
                while (args.Length >= index + 2)
                {
                    if (args[index].GetString(bubble) == "on")
                        player = args[index + 1].GetIntegerEnumerator(bubble);
                    else if (args[index].GetString(bubble) == "as")
                    {
                        var type = args[index + 1].GetString(bubble);
                        if (type == "string")
                            propertyType = typeof(string);
                        else if (type == "integer")
                            propertyType = typeof(int);
                    }

                    index += 2;
                }

                IEnumerable<int> CoEnumerateOnlyLocalPlayer() { yield return PlayerControl.LocalPlayer.PlayerId; }
                player ??= CoEnumerateOnlyLocalPlayer();

                propertyType ??= typeof(string);

                if (property == null)
                {
                    bubble.PushResponse("property must be not null.".Color(invalidColor));
                    yield break;
                }

                foreach (int playerId in player!)
                {
                    if (playerId == PlayerControl.LocalPlayer.PlayerId)
                    {
                        //ローカルなプロパティを取得
                        string? propVal = null;
                        var prop = PropertyManager.GetProperty(property!);
                        if (propertyType == typeof(int))
                            propVal = prop?.GetInteger().ToString();
                        else
                            propVal = prop?.GetString().ToString();
                        result.Value = new StaticCommandArgument(propVal ?? "Error".Color(Color.gray));
                    }
                    else
                    {
                        IEnumerator CoGetProperty<T>()
                        {
                            yield return PropertyRPC.CoGetProperty<T>((byte)playerId, property!,
                            (response) => result.Value = new StaticCommandArgument(response?.ToString() ?? "-"), () => result.Value = new StaticCommandArgument("Error".Color(Color.gray)));
                        }
                        //他プレイヤーのプロパティを取得
                        if (propertyType == typeof(int))
                            yield return CoGetProperty<int>();
                        else
                            yield return CoGetProperty<string>();
                    }
                }
            }
            return CoExecute();
        })
        , "property");
        */
    }
}

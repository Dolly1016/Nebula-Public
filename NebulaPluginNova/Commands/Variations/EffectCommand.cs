using Nebula.Commands.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.Compat;
using Virial.Game;

namespace Nebula.Commands.Variations;

public class EffectCommand : ICommand
{
    public class EffectStructure : GUICommonStructure
    {
        public float duration = 10f;
        public float ratio = 1f;
        public bool canPassMeeting = false;

        public static CommandStructureConverter<EffectStructure> Converter = new CommandStructureConverter<EffectStructure>()
            .Add<float>("duration", (structure, val) => structure.duration = Mathf.Max(val, 0))
            .Add<bool>("infinity", (structure, val) => structure.duration = val ? 10000f : structure.duration)
            .Add<float>("ratio", (structure, val) => structure.ratio = Mathf.Max(val, 0))
            .Add<bool>("canPassMeeting", (structure, val) => structure.canPassMeeting = val);

    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count != 3)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <type> <target> {structure}");

        string id = "";
        IEnumerable<GamePlayer> targets = [];
        return arguments[0].AsValue<string>(env).Action(val => id = val)
            .Chain(_ => arguments[1].AsEnumerable(env).As<GamePlayer>(env).Action(p => targets = p))
            .Chain(_ => arguments[2].AsStructure(env)).ConvertTo<EffectStructure>(EffectStructure.Converter, new(), env).ChainFast<ICommandToken, EffectStructure>(
            structure =>
            {
                if(id == "speed")
                {
                    using (RPCRouter.CreateSection("SpeedModulator"))
                    {
                        foreach (var p in targets) PlayerModInfo.RpcSpeedModulator.Invoke((p.PlayerId, new(structure.ratio, true, structure.duration, structure.canPassMeeting, 0, 0)));
                    }
                    return EmptyCommandToken.Token;
                }

                var attr = PlayerAttributeImpl.AllAttributes.FirstOrDefault(a => !a.Name.StartsWith("$") && a.Name == id);
                if(attr == null)
                {
                    env.Logger.PushError($"Unknown effectId \"{id}\"");
                    return EmptyCommandToken.Token;
                }

                using (RPCRouter.CreateSection("AttributeModulator"))
                {
                    foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new(attr, structure.duration, structure.canPassMeeting, 0, 0, true)));
                }
                return EmptyCommandToken.Token;
            }
            );
    }
}

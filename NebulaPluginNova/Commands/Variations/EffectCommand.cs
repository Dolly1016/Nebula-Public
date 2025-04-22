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
        public UnityEngine.Vector2 ratioVec = UnityEngine.Vector2.one;
        public bool canPassMeeting = true;
        public string? tag = null;
        public bool flipX, flipY, rotate;
        public bool allowDuplicate = false;

        public static CommandStructureConverter<EffectStructure> Converter = new CommandStructureConverter<EffectStructure>()
            .Add<float>("duration", (structure, val) => structure.duration = Mathf.Max(val, 0))
            .Add<bool>("infinity", (structure, val) => structure.duration = val ? 500000f : structure.duration)
            .Add<float>("ratio", (structure, val) => structure.ratio = val)
            .Add<float>("x", (structure, val) => structure.ratioVec.x = val)
            .Add<float>("y", (structure, val) => structure.ratioVec.y = val)
            .Add<string>("tag", (structure, val) => structure.tag = val)
            .Add<bool>("canPassMeeting", (structure, val) => structure.canPassMeeting = val)
            .Add<bool>("flipX", (structure, val) => structure.flipX = val)
            .Add<bool>("flipY", (structure, val) => structure.flipY = val)
            .Add<bool>("rotate", (structure, val) => structure.rotate = val)
            .Add<bool>("allowDuplicate", (structure, val) => structure.allowDuplicate = val);

    }

    IEnumerable<CommandComplement> ICommand.Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor)
    {
        return [];
    }

    CoTask<ICommandToken> ICommand.Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env)
    {
        if (CommandHelper.DenyByPermission(env, PlayerModInfo.OpPermission, out var p)) return p;

        if (arguments.Count != 3)
            return new CoImmediateErrorTask<ICommandToken>(env.Logger, label + " <type> <target> <structure>");

        string id = "";
        IEnumerable<GamePlayer> targets = [];
        return arguments[0].AsValue<string>(env).Action(val => id = val)
            .Chain(_ => arguments[1].AsEnumerable(env).As<GamePlayer>(env).Action(p => targets = p))
            .Chain(_ => arguments[2].AsStructure(env)).ConvertTo<EffectStructure>(EffectStructure.Converter, new(), env).ChainFast<ICommandToken, EffectStructure>(
            structure =>
            {
                if (id == "clear")
                {
                    if (structure.tag == null)
                    {
                        env.Logger.PushError($"Please specify the tag.");
                    }
                    else
                    {
                        using (RPCRouter.CreateSection("ClearModulator"))
                        {
                            foreach (var p in targets) PlayerModInfo.RpcRemoveAttrByTag.Invoke((p.PlayerId, structure.tag!));
                        }
                    }
                    return EmptyCommandToken.Token;
                }

                if (id == "speed")
                {
                    using (RPCRouter.CreateSection("SpeedModulator"))
                    {
                        foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new SpeedModulator(structure.ratio, structure.ratioVec, true, structure.duration, structure.canPassMeeting, 0, structure.tag), structure.allowDuplicate));
                    }
                    return EmptyCommandToken.Token;
                }

                if (id == "size")
                {
                    using (RPCRouter.CreateSection("SizeModulator"))
                    {
                        foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new SizeModulator(structure.ratioVec * structure.ratio, structure.duration, structure.canPassMeeting, 0, structure.tag), structure.allowDuplicate));
                    }
                    return EmptyCommandToken.Token;
                }

                bool CheckFloatEffect(string name, IPlayerAttribute attribute)
                {
                    if (id == name)
                    {
                        using (RPCRouter.CreateSection("Modulator"))
                        {
                            foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new FloatModulator(attribute, structure.ratio, structure.duration, structure.canPassMeeting, 0, structure.tag), structure.allowDuplicate));
                        }
                        return true;
                    }
                    return false;
                }

                if(
                    CheckFloatEffect("screen", PlayerAttributes.ScreenSize) ||
                    CheckFloatEffect("eyesight", PlayerAttributes.Eyesight) ||
                    CheckFloatEffect("grainy", PlayerAttributes.Roughening) ||
                    CheckFloatEffect("cooldown", PlayerAttributes.CooldownSpeed)
                ) return EmptyCommandToken.Token;

                if(id == "flip")
                {
                    using (RPCRouter.CreateSection("AttributeModulator"))
                    {
                        if (structure.flipX) foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new AttributeModulator(PlayerAttributes.FlipX, structure.duration, structure.canPassMeeting, 0, structure.tag, true), structure.allowDuplicate));
                        if (structure.flipY) foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new AttributeModulator(PlayerAttributes.FlipY, structure.duration, structure.canPassMeeting, 0, structure.tag, true), structure.allowDuplicate));
                        if (structure.rotate) foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new AttributeModulator(PlayerAttributes.FlipXY, structure.duration, structure.canPassMeeting, 0, structure.tag, true), structure.allowDuplicate));
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
                    foreach (var p in targets) PlayerModInfo.RpcAttrModulator.Invoke((p.PlayerId, new AttributeModulator(attr, structure.duration, structure.canPassMeeting, 0, structure.tag, true), structure.allowDuplicate));
                }
                return EmptyCommandToken.Token;
            }
            );
    }
}

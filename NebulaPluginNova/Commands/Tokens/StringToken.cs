using Virial.Command;
using Virial;
using System.Runtime.CompilerServices;
using Virial.Game;

namespace Nebula.Commands.Tokens;


/// <summary>
/// 文字列のトークンです。
/// </summary>
public class StringCommandToken : ICommandToken
{
    internal bool CanSubstitute { get; private set; } = true;
    private string myStr { get; init; }

    public string Token => myStr;

    /// <summary>
    /// 文字列トークンを生成します。
    /// </summary>
    /// <param name="text"></param>
    public StringCommandToken(string text) : this(text, true) { }

    internal StringCommandToken(string text, bool canSubstitute)
    {
        myStr = text;
        CanSubstitute = canSubstitute;
    }

    CoTask<ICommandToken> ICommandToken.EvaluateHere(CommandEnvironment env)
    {
        var applied = env.ArgumentTable.ApplyTo(this);
        if (applied == this) return new CoImmediateTask<ICommandToken>(applied);
        else return applied.EvaluateHere(env);
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        var substituted = env.ArgumentTable.ApplyTo(this);
        if (substituted == this) return  new CoImmediateTask<IEnumerable<ICommandToken>>([this]);
        return substituted.AsEnumerable(env);
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var substituted = env.ArgumentTable.ApplyTo(this);
        if (substituted != this) return substituted.AsValue<T>(env);

        var type = typeof(T);

        if (type == typeof(ICommandToken))
        {
            return new CoImmediateTask<T>(Unsafe.As<ICommandToken, T>(ref substituted));
        }
        else if (type == typeof(int))
        {
            if (int.TryParse(myStr, out var val)) return new CoImmediateTask<T>(Unsafe.As<int, T>(ref val));
            if (float.TryParse(myStr, out var valFloat))
            {
                int valInt = (int)valFloat;
                return new CoImmediateTask<T>(Unsafe.As<int, T>(ref valInt));
            }
            return new CoImmediateErrorTask<T>(env.Logger);
        }
        else if (type == typeof(float))
        {
            if (float.TryParse(myStr, out var val)) return new CoImmediateTask<T>(Unsafe.As<float, T>(ref val));
            return new CoImmediateErrorTask<T>(env.Logger);
        }
        else if (type == typeof(bool))
        {
            if (bool.TryParse(myStr, out var val)) return new CoImmediateTask<T>(Unsafe.As<bool, T>(ref val));
            return new CoImmediateErrorTask<T>(env.Logger);
        }
        else if (type == typeof(string))
        {
            var temp = myStr;
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref temp));
        }
        else if (type == typeof(Virial.Game.Player))
        {
            var temp = NebulaAPI.CurrentGame?.GetAllPlayers().FirstOrDefault(p => p.Name == myStr);
            if (temp != null) return new CoImmediateTask<T>(Unsafe.As<Virial.Game.Player, T>(ref temp));
        }
        else if (type == typeof(OutfitDefinition))
        {
            var player = NebulaAPI.CurrentGame?.GetAllPlayers().FirstOrDefault(p => p.Name == myStr);
            NebulaGameManager.Instance.TryGetOutfit(OutfitDefinition.OutfitId.PlayersDefault(player?.PlayerId ?? 0), out var temp);
            if (temp != null) return new CoImmediateTask<T>(Unsafe.As<OutfitDefinition, T>(ref temp));
        }
        else if (type == typeof(Virial.Assignable.DefinedRole))
        {
            Virial.Assignable.DefinedRole? role = Roles.Roles.AllRoles.FirstOrDefault(r => r.LocalizedName == myStr);
            if(role != null) return new CoImmediateTask<T>(Unsafe.As<Virial.Assignable.DefinedRole, T>(ref role));
            return new CoImmediateErrorTask<T>();
        }
        else if (type == typeof(Virial.Assignable.DefinedModifier))
        {
            Virial.Assignable.DefinedModifier? role = Roles.Roles.AllModifiers.FirstOrDefault(r => r.LocalizedName == myStr);
            if (role != null) return new CoImmediateTask<T>(Unsafe.As<Virial.Assignable.DefinedModifier, T>(ref role));
            return new CoImmediateErrorTask<T>();
        }
        else if(type == typeof(ICommand))
        {
            if(CommandManager.TryGetCommand(myStr, out var command))
                return new CoImmediateTask<T>(Unsafe.As<ICommand, T>(ref command));
            return new CoImmediateErrorTask<T>();
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }

    IExecutable? ICommandToken.ToExecutable(CommandEnvironment env)
    {
        var substituted = env.ArgumentTable.ApplyTo(this);
        if (substituted != this) return substituted.ToExecutable(env);

        return null;
    }
}


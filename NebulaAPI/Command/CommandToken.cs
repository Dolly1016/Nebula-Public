using Epic.OnlineServices.Auth;
using Il2CppSystem.CodeDom.Compiler;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;

namespace Virial.Command;

/// <summary>
/// コマンドのトークン列を構成します。
/// </summary>
public interface ICommandToken
{
    /// <summary>
    /// この引数を列挙可能な値として捉え、その中身を列挙させます。
    /// 列挙可能な値でないものは、自身1つを列挙するのが妥当です。
    /// </summary>
    /// <returns></returns>
    CoTask<IEnumerable<ICommandToken>> AsEnumerable(CommandEnvironment env) => new CoImmediateTask<IEnumerable<ICommandToken>>([this]);

    /// <summary>
    /// この引数を値としてとらえ、その中身を取り出します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    CoTask<T> AsValue<T>(CommandEnvironment env);

    /// <summary>
    /// この値を構造体ととらえ、その構造を評価し、取得します。
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="executor"></param>
    /// <param name="argumentTable"></param>
    /// <returns></returns>
    CoTask<CommandStructure> AsStructure(CommandEnvironment env) => AsValue<CommandStructure>(env);

    /// <summary>
    /// 指定された引数の環境の下で評価します。
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="executor"></param>
    /// <param name="argumentTable"></param>
    /// <returns></returns>
    CoTask <ICommandToken> EvaluateHere(CommandEnvironment env) => new CoImmediateTask<ICommandToken>(this);

    /// <summary>
    /// 実行可能なオブジェクトに変換します。
    /// </summary>
    /// <param name="env"></param>
    /// <returns></returns>
    IExecutable? ToExecutable(CommandEnvironment env) => null;
}

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
        return new CoImmediateTask<ICommandToken>(env.ArgumentTable.ApplyTo(this));
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>([env.ArgumentTable.ApplyTo(this)]);
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var substituted = env.ArgumentTable.ApplyTo(this);
        if (substituted != this) return substituted.AsValue<T>(env);

        var type = typeof(T);

        if (type == typeof(int))
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
        else if (type == typeof(Game.Player))
        {
            var temp = NebulaAPI.CurrentGame?.GetAllPlayers().FirstOrDefault(p => p.Name == myStr);
            if(temp != null) return new CoImmediateTask<T>(Unsafe.As<Game.Player, T>(ref temp));
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

/// <summary>
/// 配列のトークンです
/// </summary>
public class ArrayCommandToken : ICommandToken
{
    private IReadOnlyArray<ICommandToken> tokens { get; init; }

    /// <summary>
    /// 配列トークンを生成します。
    /// </summary>
    /// <param name="text"></param>
    public ArrayCommandToken(IReadOnlyArray<ICommandToken> arguments)
    {
        this.tokens = arguments ?? new ReadOnlyArray<ICommandToken>(Array.Empty<ICommandToken>());
    }

    CoTask<ICommandToken> ICommandToken.EvaluateHere(CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(tokens).Select(token => token.EvaluateHere(env))
            .Chain(result => new CoImmediateTask<ICommandToken>(new ArrayCommandToken(new ReadOnlyArray<ICommandToken>(result))));
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(tokens)
            .Select(token => token.AsEnumerable(env))
            .Chain(result =>
            {
                List<ICommandToken> list = new();
                foreach (var r in result) list.AddRange(r);
                return new CoImmediateTask<IEnumerable<ICommandToken>>(list);
            });
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

/// <summary>
/// 構造体トークンです。
/// </summary>
public class StructCommandToken : ICommandToken
{
    private IReadOnlyArray<(ICommandToken label, ICommandToken value)> members { get; init; }

    /// <summary>
    /// 構造体トークンを生成します。
    /// </summary>
    /// <param name="text"></param>
    public StructCommandToken(IReadOnlyArray<(ICommandToken label, ICommandToken value)> members)
    {
        this.members = members;
    }

    IEnumerator CoEvaluate<T>((ICommandToken label, ICommandToken value) member, CoBuiltInTask<(T label, ICommandToken value)> myResult, CommandEnvironment env)
    {
        CoTask<T> labelTask;
        if (typeof(T) is ICommandToken)
        {
            var task = member.label.EvaluateHere(env);
            labelTask = Unsafe.As<CoTask<ICommandToken>, CoTask<T>>(ref task);
        }
        else
        {
            labelTask = member.label.AsValue<T>(env);
        }

        yield return labelTask.CoWait();
        var valueTask = member.value.EvaluateHere(env);
        yield return valueTask.CoWait();
        if (labelTask.IsFailed || valueTask.IsFailed)
        {
            myResult.IsFailed = true;
            yield break;
        }
        myResult.Result = (labelTask.Result, valueTask.Result);
        yield break;
    }

    CoTask<ICommandToken> ICommandToken.EvaluateHere(CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<(ICommandToken label, ICommandToken value)>>(members)
            .Select(val => new CoBuiltInTask<(ICommandToken label, ICommandToken value)>(task => CoEvaluate(val,task, env)))
            .Chain(val => new CoImmediateTask<ICommandToken>(new StructCommandToken(new ReadOnlyArray<(ICommandToken label,ICommandToken value)>(val))));
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        env.Logger.PushError("Struct Tokens can be not evaluated as enumerable.");
        throw new Exception();
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var type = typeof(T);

        if (type == typeof(CommandStructure))
        {
            return new CoImmediateTask<IEnumerable<(ICommandToken label, ICommandToken value)>>(members)
           .Select(val => new CoBuiltInTask<(string label, ICommandToken value)>(task => CoEvaluate(val, task, env)))
           .Chain(val =>
           {
               Dictionary<string, ICommandToken> doc = new();
               foreach (var entry in val) doc[entry.label] = entry.value;
               var structure = new CommandStructure(doc);
               return new CoImmediateTask<T>(Unsafe.As<CommandStructure, T>(ref structure));
           });
        }

        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

/// <summary>
/// 空のトークンです
/// </summary>
public class EmptyCommandToken : ICommandToken
{
    public static ICommandToken Token = new EmptyCommandToken(); 

    /// <summary>
    /// 配列トークンを生成します。
    /// </summary>
    public EmptyCommandToken()　{}

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(Array.Empty<ICommandToken>());
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        return new CoImmediateErrorTask<T>(env.Logger);
    }
}

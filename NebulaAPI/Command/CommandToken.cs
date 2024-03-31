using System;
using System.Collections.Generic;
using System.Linq;
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
    CoTask<IEnumerable<ICommandToken>> AsEnumerable(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable) => new CoImmediateTask<IEnumerable<ICommandToken>>([this]);

    /// <summary>
    /// この引数を値としてとらえ、その中身を取り出します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    CoTask<T> AsValue<T>(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable);

    /// <summary>
    /// 指定された引数の環境の下で評価します。
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="executor"></param>
    /// <param name="argumentTable"></param>
    /// <returns></returns>
    CoTask<ICommandToken> EvaluateHere(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable) => new CoImmediateTask<ICommandToken>(this);
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

    CoTask<ICommandToken> ICommandToken.EvaluateHere(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateTask<ICommandToken>(argumentTable.ApplyTo(this));
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>([argumentTable.ApplyTo(this)]);
    }

    CoTask<T> ICommandToken.AsValue<T>(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        var substituted = argumentTable.ApplyTo(this);
        if (substituted != this) return substituted.AsValue<T>(logger, executor, argumentTable);

        var type = typeof(T);

        if (type == typeof(int))
        {
            if (int.TryParse(myStr, out var val)) return new CoImmediateTask<T>(Unsafe.As<int, T>(ref val));
            return new CoImmediateErrorTask<T>(logger);
        }
        else if (type == typeof(float))
        {
            if (float.TryParse(myStr, out var val)) return new CoImmediateTask<T>(Unsafe.As<float, T>(ref val));
            return new CoImmediateErrorTask<T>(logger);
        }
        else if (type == typeof(bool))
        {
            if (bool.TryParse(myStr, out var val)) return new CoImmediateTask<T>(Unsafe.As<bool, T>(ref val));
            return new CoImmediateErrorTask<T>(logger);
        }
        else if (type == typeof(string))
        {
            var temp = myStr;
            return new CoImmediateTask<T>(Unsafe.As<string, T>(ref temp));
        }

        return new CoImmediateErrorTask<T>(logger);
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

    CoTask<ICommandToken> ICommandToken.EvaluateHere(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(tokens).Select(token => token.EvaluateHere(logger, executor, argumentTable))
            .Chain(result => new CoImmediateTask<ICommandToken>(new ArrayCommandToken(new ReadOnlyArray<ICommandToken>(result))));
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(tokens)
            .Select(token => token.AsEnumerable(logger, executor, argumentTable))
            .Chain(result =>
            {
                List<ICommandToken> list = new();
                foreach (var r in result) list.AddRange(r);
                return new CoImmediateTask<IEnumerable<ICommandToken>>(list);
            });
    }

    CoTask<T> ICommandToken.AsValue<T>(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateErrorTask<T>(logger);
    }
}

/// <summary>
/// 空のトークンです
/// </summary>
public class EmptyCommandToken : ICommandToken
{

    /// <summary>
    /// 配列トークンを生成します。
    /// </summary>
    public EmptyCommandToken()　{}

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateTask<IEnumerable<ICommandToken>>(Array.Empty<ICommandToken>());
    }

    CoTask<T> ICommandToken.AsValue<T>(ICommandLogger logger, ICommandExecutor executor, ICommandModifier argumentTable)
    {
        return new CoImmediateErrorTask<T>(logger);
    }
}

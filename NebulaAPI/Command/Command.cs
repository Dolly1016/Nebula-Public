using System.Collections;
using System.Runtime.CompilerServices;
using Virial.Compat;
using System.Diagnostics.CodeAnalysis;
using Virial.Common;

namespace Virial.Command;

public class CommandStructure
{
    private Dictionary<string, ICommandToken> members;

    internal CommandStructure(Dictionary<string, ICommandToken>? members = null) { this.members = members ?? new(); }

    public ICommandToken this[string label] { get => members[label]; }

    public bool TryGetValue(string label, [MaybeNullWhen(false)] out ICommandToken value) => members.TryGetValue(label, out value);
    public IEnumerable<string> Labels => members.Keys;
}

public static class CommandStructureHelper
{
    public static CoTask<T> ConvertTo<T>(this CoTask<CommandStructure> task, CommandStructureConverter<T> converter, T target, CommandEnvironment env)
    {
        return converter.ChainConverterTo(task, target, env);
    }
}

public class CommandStructureConverter<T>
{
    List<Func<T,CommandEnvironment, CommandStructure, CoTask<ICommandToken>>> suppliers = new();

    /// <summary>
    /// 親クラスの変換器の定義をそのまま継承します。
    /// </summary>
    /// <typeparam name="V">親クラス</typeparam>
    /// <param name="converter"></param>
    /// <returns></returns>
    public CommandStructureConverter<T> Inherit<V>(CommandStructureConverter<V> converter)
    {
        //親の型でないなら無視する
        if (typeof(T).IsAssignableTo(typeof(V)))
        {
            suppliers.Add((t, env, structure) =>
                converter.ChainConverterTo(new CoImmediateTask<CommandStructure>(structure), Unsafe.As<T, V>(ref t), env).Discard<ICommandToken, V>());
        }

        return this;
    }

    public CommandStructureConverter<T> Add<V>(string label, Action<T,V> setter)
    {
        bool isBool = typeof(V) == typeof(bool);

        suppliers.Add((t, env, structure) =>
        {
            if (structure.TryGetValue(label, out var token))
            {
                if (typeof(V) == typeof(IExecutable))
                {
                    var executable = token.ToExecutable(env);
                    if(executable != null)
                        return new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token).Action(_ => setter.Invoke(t, Unsafe.As<IExecutable, V>(ref executable)));
                    return new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token);
                }
                return token.AsValue<V>(env).Action(v => setter.Invoke(t, v));
            }
            else if(isBool && structure.TryGetValue("~" + label, out var invToken))
            {
                return invToken.AsValue<bool>(env).Action(v => {
                    v = !v;
                    setter.Invoke(t, Unsafe.As<bool, V>(ref v));
                });
            }
            else
                return new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token);
        });
        return this;
    }

    public CommandStructureConverter<T> AddCollection<V>(string label, Action<T, IEnumerable<V>> setter)
    {
        suppliers.Add((t, env, structure) =>
        {
            if (structure.TryGetValue(label, out var token))
                return token.AsEnumerable(env).Select(token => token.AsValue<V>(env)).Action(v => setter.Invoke(t, v));
            else
                return new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token);
        });
        return this;
    }

    public CommandStructureConverter<T> AddStructure<V>(string label, CommandStructureConverter<V> converter, Func<V> constructor, Action<T,V> setter)
    {
        suppliers.Add((t, env, structure) =>
        {
            if (structure.TryGetValue(label, out var token))
            {
                return converter.ChainConverterTo(token.AsStructure(env), constructor.Invoke(), env).Action(val => setter.Invoke(t, val));
            }
            else
                return new CoImmediateTask<ICommandToken>(EmptyCommandToken.Token);
        });
        return this;
    }

    public CoTask<T> ChainConverterTo(CoTask<CommandStructure> task, T target, CommandEnvironment env)
    {
        return task.DoParallel(suppliers
            .Select<Func<T, CommandEnvironment, CommandStructure, CoTask<ICommandToken>>, Func<CommandStructure, CoTask<ICommandToken>>>
            (s => (CommandStructure structure) => s.Invoke(target, env, structure)).ToArray())
            .ChainFast(_ => target);
    }
}

public record CommandEnvironment(ICommandExecutor Executor, ICommandModifier ArgumentTable, ICommandLogger Logger)
{
    public CommandEnvironment SwitchArgumentTable(ICommandModifier newTable) => new CommandEnvironment(Executor, newTable, Logger);
}

/// <summary>
/// コマンドの実行者を表します。
/// </summary>
public interface ICommandExecutor : IPermissionHolder
{
}

/// <summary>
/// コマンド修飾子
/// 変数の代入を担います。
/// </summary>
public interface ICommandModifier
{
    internal ICommandToken ApplyTo(ICommandToken argument);
}

[Flags]
public enum CommandLogLevel
{
    Info        = 0x01,
    Warning     = 0x02,
    Error       = 0x04,
    AllLevel    = 0xFF
}

public interface ICommandLogText
{
    internal bool IsDirty { get; set; }

    /// <summary>
    /// 現在の文字列を取得します。
    /// </summary>
    string CurrentText { get; }

    CommandLogLevel LogLevel { get; set; }

    /// <summary>
    /// 出力されている文字列を変更します。
    /// </summary>
    /// <param name="newText"></param>
    void UpdateText(string newText);
}


/// <summary>
/// コマンド用のロガーです。
/// </summary>
public interface ICommandLogger
{
    internal bool IsDirty { get; }
    /// <summary>
    /// 更新済みにマークし、IsDirtyのフラグを下ろします。
    /// </summary>
    internal void MarkUpdated();
    /// <summary>
    /// ロガーにテキストを追加します。
    /// </summary>
    /// <param name="logLevel"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    ICommandLogText Push(CommandLogLevel logLevel, string message);

    /// <summary>
    /// ロガーにログテキストを追加します。
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    ICommandLogText Push(string message) => Push(CommandLogLevel.Info, message);

    /// <summary>
    /// ロガーにエラーテキストを追加します。
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    ICommandLogText PushError(string message) => Push(CommandLogLevel.Error, message);

    /// <summary>
    /// 出力を削除します。
    /// </summary>
    /// <param name="text"></param>
    void Remove(ICommandLogText text);

    /// <summary>
    /// ログ出力用の文字列を求めます。
    /// </summary>
    /// <returns></returns>
    string ToLogString(CommandLogLevel logLevelMask);

    string Executed { get; }
}

/// <summary>
/// コマンドの補完候補です。
/// </summary>
/// <param name="text">補完する全テキスト</param>
/// <param name="withBoxing">ダブルクォーテーションで囲む場合はtrue</param>
public record CommandComplement(string text, bool withBoxing);

public interface ICommand
{
    /// <summary>
    /// コマンドを実行します。
    /// </summary>
    /// <param name="label">コマンド実行時のコマンド名</param>
    /// <param name="arguments">与えられた引数</param>
    /// <param name="argumentTable">与えられた引数テーブル</param>
    /// <param name="executor">コマンドの実行者</param>
    /// <returns></returns>
    CoTask<ICommandToken> Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, CommandEnvironment env);

    /// <summary>
    /// 入力中の文字を補完する候補を返します。
    /// </summary>
    /// <param name="label"></param>
    /// <param name="arguments"></param>
    /// <param name="last"></param>
    /// <param name="executor">予見されるコマンドの実行者</param>
    /// <returns></returns>
    IEnumerable<CommandComplement> Complement(string label, IReadOnlyArray<ICommandToken> arguments, string? last, ICommandExecutor executor);
}

/// <summary>
/// コマンド文節の定義です。
/// </summary>
/// <param name="label"></param>
/// <param name="length"></param>
/// <param name="processor"></param>
public record CommandClause(string label, int length, TaskSupplier<bool, IReadOnlyArray<ICommandToken>> processor);

/// <summary>
/// コマンドに関するコーディングをサポートする関数を提供します。
/// </summary>
public static class NebulaCommandHelper
{
    /// <summary>
    /// 全プレイヤー名の候補を返します。
    /// </summary>
    public static IEnumerable<CommandComplement> AllPlayersNameComplement =>
        NebulaAPI.CurrentGame?.GetAllPlayers().Select(p => new CommandComplement(p.Name, p.Name.Contains(' '))) ?? Enumerable.Empty<CommandComplement>();

    /// <summary>
    /// 条件に適うプレイヤー名の候補を返します。
    /// </summary>
    public static IEnumerable<CommandComplement> PlayersNameComplement(Predicate<Game.Player>? predicate = null) =>
        NebulaAPI.CurrentGame?.GetAllPlayers().Where(p => predicate?.Invoke(p) ?? true).Select(p => new CommandComplement(p.Name, p.Name.Contains(' '))) ?? Enumerable.Empty<CommandComplement>();

    public static CoTask<bool> InterpretClause(IReadOnlyArray<ICommandToken> tokens, IEnumerable<CommandClause> clauses, CommandEnvironment env)
    {
        IEnumerator CoExecute(CoBuiltInTask<bool> myTask)
        {
            myTask.Result = true;
            for (int i= 0;i<tokens.Count;i++)
            {
                var token = tokens[i];
                var labelTask = token.AsValue<string>(env);
                yield return labelTask.CoWait();
                if (labelTask.IsFailed)
                {
                    env.Logger.PushError("Unknown label was received at " + i + ".");
                    myTask.IsFailed = true;
                    break;
                }
                var clause = clauses.FirstOrDefault(c => c.label == labelTask.Result);

                if(clause == null)
                {
                    env.Logger.PushError($"Unresolvable label \"{labelTask.Result}\".");
                    myTask.IsFailed = true;
                    break;
                }

                if(clause.length > tokens.Count - i)
                {
                    env.Logger.PushError($"Clause \"{labelTask.Result}\" requires {clause.length} tokens.");
                    myTask.IsFailed = true;
                    break;
                }

                var processTask = clause.processor.Invoke(tokens.Slice(i + 1, clause.length));
                i += clause.length;
                yield return processTask.CoWait();
            }
        }
        return new CoBuiltInTask<bool>(CoExecute);
    }
}
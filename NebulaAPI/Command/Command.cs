using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Steamworks;
using Virial.Compat;

namespace Virial.Command;

/// <summary>
/// コマンドの実行者を表します。
/// </summary>
public interface ICommandExecutor
{
    bool IsOp { get; }
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
    CoTask<ICommandToken> Evaluate(string label, IReadOnlyArray<ICommandToken> arguments, ICommandModifier argumentTable, ICommandExecutor executor, ICommandLogger logger);

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
}
using Virial.Command;
using Virial.Compat;

namespace Nebula.Commands.Tokens;

/// <summary>
/// コマンドを実行するトークンです
/// </summary>
public class StatementCommandToken : ICommandToken
{
    private IReadOnlyArray<ICommandToken> tokens { get; init; }

    public StatementCommandToken(IReadOnlyArray<ICommandToken> arguments)
    {
        this.tokens = arguments;
    }

    public IEnumerable<ICommandToken> RawTokens => tokens;

    CoTask<ICommandToken> ICommandToken.EvaluateHere(CommandEnvironment env)
    {
        //return new CoImmediateTask<IEnumerable<ICommandToken>>(this.tokens).SelectParallel(t => t.EvaluateHere(env)).ChainFast<ICommandToken, IEnumerable<ICommandToken>>(col => new StatementCommandToken(new ReadOnlyArray<ICommandToken>(col)));
        return (this as ICommandToken)?.ToExecutable(env)?.CoExecute([]) ?? new CoImmediateErrorTask<ICommandToken>();
    }

    CoTask<IEnumerable<ICommandToken>> ICommandToken.AsEnumerable(CommandEnvironment env)
    {
        var commandTask = CommandManager.CoExecute(tokens, env);
        return new CoChainedTask<IEnumerable<ICommandToken>, ICommandToken>(commandTask, result => result.AsEnumerable(env));
    }

    CoTask<T> ICommandToken.AsValue<T>(CommandEnvironment env)
    {
        var commandTask = CommandManager.CoExecute(tokens, env);
        return new CoChainedTask<T, ICommandToken>(commandTask, result => result.AsValue<T>(env));
    }

    IExecutable? ICommandToken.ToExecutable(CommandEnvironment env) => new CommandExecutable(this, env);
}

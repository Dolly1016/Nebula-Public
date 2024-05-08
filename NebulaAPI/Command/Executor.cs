namespace Virial.Command;

/// <summary>
/// 実行可能な対象を表します。
/// </summary>
public interface IExecutable
{
    CoTask<ICommandToken> CoExecute((string label, ICommandToken token)[] extra);
}

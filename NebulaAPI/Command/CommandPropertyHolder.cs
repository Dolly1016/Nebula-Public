namespace Virial.Command;

public interface ICommandPropertyHolder
{
    /// <summary>
    /// プロパティを取得します。
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    ICommandToken? GetCommandProperty(string propertyName);
}

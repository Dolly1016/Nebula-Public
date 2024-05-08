using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Text;

namespace Virial.Runtime;



public interface NebulaPreprocessor
{
    /// <summary>
    /// RPCで送受信できる翻訳テキストのタグを生成します。
    /// このタグはプリプロセスでのみ生成できます。
    /// </summary>
    /// <param name="translationKey"></param>
    /// <returns></returns>
    CommunicableTextTag RegisterCommunicableText(string translationKey);

    /// <summary>
    /// クライアント間で共有できる変数を生成します。
    /// この変数はプリプロセスでのみ生成できます。
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    ISharableEntry RegisterSharableEntry(string id);

    /// <summary>
    /// 新たなプレイヤーアサイナブルを追加します。
    /// 役職かモディファイア、幽霊役職である必要があります。
    /// </summary>
    /// <param name="assignable"></param>
    void RegisterAssignable(DefinedAssignable assignable);

    /// <summary>
    /// 新たなロールチームを追加します。
    /// </summary>
    /// <param name="translationKey"></param>
    /// <param name="color"></param>
    /// <param name="revealType"></param>
    /// <returns></returns>
    RoleTeam CreateTeam(string translationKey, Color color, TeamRevealType revealType);

    DIManager DIManager { get; }
}

public interface NebulaRuntime
{

}
using System.Collections;
using Virial.Assignable;
using Virial.Attributes;
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
    //CommunicableTextTag RegisterCommunicableText(string translationKey);

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

    void SchedulePreprocess(PreprocessPhase phase, Action process);
    void SchedulePreprocess(PreprocessPhase phase, IEnumerator process);

    /// <summary>
    /// ローディング中のテキストを差し替えます。
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    IEnumerator SetLoadingText(string text);

    internal IEnumerator RunPreprocess(PreprocessPhase preprocess);
    internal void PickUpPreprocess(System.Reflection.Assembly assembly);

    public bool FinishPreprocess { get; }
}

public interface NebulaRuntime
{

}
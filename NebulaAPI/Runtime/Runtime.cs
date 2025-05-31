using System;
using System.Collections;
using Virial.Assignable;
using Virial.Attributes;
using Virial.DI;
using Virial.Game;
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

    /// <summary>
    /// 新たなゲーム終了を追加します。
    /// </summary>
    /// <param name="localizedName">ゲーム終了の翻訳名。他のゲーム終了と重複しない名前を付けてください。</param>
    /// <param name="color">色。</param>
    /// <param name="priority">勝利の優先度。この値が大きいほど優先されます。</param>
    /// <returns></returns>
    GameEnd CreateEnd(string localizedName, Color color, int priority = 32);
    /// <summary>
    /// 新たなゲーム終了を追加します。
    /// </summary>
    /// <param name="immutableId">ゲーム終了の不変な内部名。他のゲーム終了と重複しない名前を付けてください。</param>
    /// <param name="displayText">ゲーム終了の表示テキスト。</param>
    /// <param name="color">色。</param>
    /// <param name="priority">勝利の優先度。この値が大きいほど優先されます。</param>
    /// <returns></returns>
    GameEnd CreateEnd(string immutableId, TextComponent displayText, Color color, int priority = 32);
    /// <summary>
    /// 新たな追加勝利を追加します。
    /// </summary>
    /// <param name="localizedName">追加勝利の翻訳名。他の追加勝利と重複しない名前を付けてください。</param>
    /// <param name="color">色。</param>
    /// <returns></returns>
    ExtraWin CreateExtraWin(string localizedName, Color color);
    /// <summary>
    /// 新たな追加勝利を追加します。
    /// </summary>
    /// <param name="immutableId">追加勝利の不変な内部名。他のゲーム終了と重複しない名前を付けてください。</param>
    /// <param name="displayText">追加勝利の表示テキスト。</param>
    /// <param name="color">色。</param>
    /// <returns></returns>
    ExtraWin CreateExtraWin(string immutableId, TextComponent displayText, Color color);

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
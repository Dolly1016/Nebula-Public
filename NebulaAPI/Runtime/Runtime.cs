using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
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
}

public interface NebulaRuntime
{

}
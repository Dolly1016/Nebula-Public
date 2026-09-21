using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Command;
using Virial.DI;

namespace Virial.Game;

public interface IFakePlayer : IPlayerlike, IReleasable
{
    /// <summary>
    /// このニセモノを呼び出したプレイヤー。分からない場合はnull。
    /// </summary>
    /// <remarks>
    /// 見た目の元になったプレイヤーを表す <see cref="IPlayerlike.RealPlayer"/> とは別物です。
    /// 彫刻家のように、他人の姿をしたニセモノを呼び出す役職があります。
    /// </remarks>
    Player? Owner => null;

    /// <summary>
    /// このニセモノが湧いた理由。分からない場合はnull。
    /// </summary>
    Virial.Text.CommunicableTextTag? SpawnReason => null;
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Game;

/// <summary>
/// キルはホストが処理します。
/// キルのリクエストから実際のキルの遂行までにタイムラグがあるため、この間に生死の状態に変化が起きている場合があります。
/// 実際のキルを遂行するか否かを決定する際に条件を課します。
/// </summary>
[Flags]
public enum KillCondition
{
    NoCondition = 0x00,
    KillerAlive = 0x01,
    TargetAlive = 0x02,
    BothAlive = KillerAlive | TargetAlive,
}

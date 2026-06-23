using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;

namespace Virial.Achievements;

internal interface ITitleHandler : IGenericModule<Game.Game>
{
    /// <summary>
    /// 称号獲得の進捗を進めます。
    /// </summary>
    /// <param name="id">称号のID</param>
    void Progress(string id);

    /// <summary>
    /// 称号獲得の進捗を進めます。
    /// </summary>
    /// <param name="id">称号のID</param>
    /// <param name="num">進捗。Goalが2以上の進捗で有効です。</param>
    void Progress(string id, int num);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.DI;

namespace Virial.Game;

public interface IWinningOpportunity : IGenericModule<Game>
{
    /// <summary>
    /// 勝機を更新します。
    /// </summary>
    /// <param name="team"></param>
    /// <param name="opportunity"></param>
    /// <param name="isMomentary">瞬間的な勝機であればtrue</param>
    void SetOpportunity(RoleTeam team, float opportunity, bool isMomentary = false);

    /// <summary>
    /// 勝機の更新を送信します。全クライアントで勝機が更新されます。
    /// </summary>
    /// <param name="team"></param>
    /// <param name="opportunity"></param>
    /// <param name="isMomentary">瞬間的な勝機であればtrue</param>
    void RpcSetOpportunity(RoleTeam team, float opportunity, bool isMomentary = false);

    /// <summary>
    /// 勝機を取得します。
    /// </summary>
    /// <param name="team"></param>
    float GetOpportunity(RoleTeam team);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Assignable;

/// <summary>
/// 役職の割り当て器です。
/// ゲーム開始時に生成され、ゲーム終了まで同じ割り当て器が使用されます。
/// </summary>
public interface IRoleAllocator
{
    /// <summary>
    /// 役職を割り当てます。誰がインポスターになるべきかのみ決定しています。
    /// </summary>
    /// <param name="impostors"></param>
    /// <param name="others"></param>
    public abstract void Assign(List<byte> impostors, List<byte> others);

    /// <summary>
    /// 幽霊役職の割り当てを試行します。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public virtual DefinedGhostRole? AssignToGhost(Virial.Game.Player player) => null;
}

/// <summary>
/// 役職の割り当てテーブルです。
/// ホストが割り当てを確定すると全プレイヤーに役職が配られ、ゲームが開始します。
/// </summary>
public interface IRoleTable
{
    /// <summary>
    /// 陣営別の割り当てを取得します。
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    IEnumerable<(byte playerId, DefinedRole role)> GetPlayers(RoleCategory category);

    /// <summary>
    /// プレイヤーに役職を割り当てます。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="role"></param>
    /// <param name="arguments"></param>

    void SetRole(byte player, DefinedRole role, int[]? arguments = null);

    /// <summary>
    /// プレイヤーにモディファイアを割り当てます。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="role"></param>
    /// <param name="arguments"></param>
    void SetModifier(byte player, DefinedModifier role, int[]? arguments = null);

    /// <summary>
    /// 割り当てを確定します。
    /// </summary>
    void Determine();
}

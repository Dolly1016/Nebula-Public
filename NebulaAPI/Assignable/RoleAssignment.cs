using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Assignable;

public record AssignmentCandidate(int Id, byte PlayerId, DefinedRole Role);

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
    void Assign(List<byte> impostors, List<byte> others);

    IRoleDraftAllocator? GetDraftAllocator() => null;

    /// <summary>
    /// 幽霊役職の割り当てを試行します。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    DefinedGhostRole? AssignToGhost(Virial.Game.Player player) => null;
}

/// <summary>
/// ドラフト形式で役職を割り当てます。
/// </summary>
public interface IRoleDraftAllocator
{
    /// <summary>
    /// 指定のプレイヤーのために役職をいくつか抽選します。
    /// 十分な抽選ができないと判断した場合、nullが返ります。
    /// リストが返された場合、このあとPopAsを呼び出して割り当てられる役職を確定する必要があります。
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns></returns>
    List<AssignmentCandidate>? Peek(byte playerId, int candidateNum);

    /// <summary>
    /// 割り当てる役職を確定します。
    /// </summary>
    /// <param name="candidate"></param>
    void PopAs(AssignmentCandidate candidate);
    /// <summary>
    /// 指定されたプレイヤーをランダム割り当てのプレイヤーとして設定します。
    /// </summary>
    /// <param name="playerId"></param>
    void SetRandom(byte playerId);
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
    /// 役職ごとに割り当てを取得します。
    /// </summary>
    /// <returns></returns>
    IEnumerable<byte> GetPlayers(DefinedRole role);

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
    /// 既存の割り当てを編集します。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="editor"></param>
    void EditRole(byte player, Func<(DefinedRole role, int[] argument), (DefinedRole role, int[]? argument)> editor);

    /// <summary>
    /// 割り当てを確定します。
    /// </summary>
    internal void Determine();
}

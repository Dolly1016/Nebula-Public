using System.Collections.Generic;
using System.Linq;
using Nebula.Player;
using Virial.Game;

namespace Nebula.Game;

/// <summary>
/// リザルト画面に出す文字列を組み立てる。
/// </summary>
/// <remarks>
/// 同じ文字列を記録にも残すので、組み立てはここ 1 箇所に集める。
/// リザルト画面と記録で見え方が食い違わないようにするための置き場所である。
/// スプライトタグや色タグを含んだままの文字列を返す。
/// </remarks>
internal static class ResultText
{
    /// <summary>役職の変遷を含む表示名。</summary>
    public static string RoleText(IReadOnlyList<RoleHistory> history, byte playerId)
    {
        if (history.Count == 0) return "";

        var entries = history.EachMoment(h => h.PlayerId == playerId,
            (role, ghostRole, modifiers) => (
                RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, true),
                RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, false))).ToArray();
        if (entries.Length == 0) return "";

        string text = "";
        if (entries.Length < 5)
        {
            for (int i = 0; i < entries.Length - 1; i++)
            {
                if (text.Length > 0) text += " → ";
                text += entries[i].Item1;
            }
        }
        else
        {
            text = entries[0].Item1 + " → ...";
        }

        if (text.Length > 0) text += " → ";
        return text + entries[^1].Item2;
    }

    /// <summary>
    /// 役職やアビリティが持つ追加情報。何も無ければ空。
    /// </summary>
    /// <remarks>
    /// 役職・幽霊役職・モディファイアのそれぞれが名乗る分をまとめる。
    /// 既定の実装が空文字を返すことがあるので、中身のあるものだけを繋ぐ。
    /// </remarks>
    public static string MoreInformation(GamePlayer player) =>
        string.Join(", ", player.AllAssigned()
            .Select(assigned => assigned.MoreInformation)
            .Where(text => !string.IsNullOrWhiteSpace(text)));

    /// <summary>タスクの進み具合。タスクを持たない場合と切断した場合は空。</summary>
    public static string TaskText(GamePlayer player)
    {
        if (player.IsDisconnected || player.Tasks.Quota <= 0) return "";

        //色でクルーメイトのタスクか偽のタスクかを見分けられるようにする。
        return $"({player.Tasks.Unbox().ToString(true)})"
            .Color(player.Tasks.IsCrewmateTask ? PlayerModInfo.CrewTaskColor : PlayerModInfo.FakeTaskColor);
    }
}

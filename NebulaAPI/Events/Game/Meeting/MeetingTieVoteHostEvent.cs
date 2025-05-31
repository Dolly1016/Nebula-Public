using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// タイ投票が発生したときに発火します。
/// 追加票を投じさせられます。追加票を投じた後の投票結果がタイ投票であっても、このイベントは発火しません。
/// </summary>
/// <remarks>
/// このイベントはホストでのみ発火します。
/// </remarks>
public class MeetingTieVoteHostEvent : Event
{
    private Dictionary<byte, Virial.Game.Player?> voteForMap;
    internal List<Virial.Game.Player?> ExtraVotes { get; private init; } = new();

    internal MeetingTieVoteHostEvent(Dictionary<byte, Virial.Game.Player?> voteForMap)
    {
        this.voteForMap = voteForMap;
    }

    internal bool TryCheckVotedFor(byte voterId, out Virial.Game.Player? votedFor) { 
        return voteForMap.TryGetValue(voterId, out votedFor);
    }

    /// <summary>
    /// 投票者の投票先を取得します。
    /// </summary>
    /// <param name="voter">投票者</param>
    /// <param name="votedFor">投票先がある場合、投票先。スキップ票及び白票の場合は<c>null</c></param>
    /// <returns></returns>
    public bool TryCheckVotedFor(Virial.Game.Player voter, out Virial.Game.Player? votedFor) => TryCheckVotedFor(voter.PlayerId, out votedFor);

    /// <summary>
    /// 追加票を投じます。
    /// </summary>
    /// <param name="voteFor"></param>
    public void AddExtraVote(Virial.Game.Player? voteFor) => ExtraVotes.Add(voteFor);
}

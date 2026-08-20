using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 投票の集計を開始する直前に、一度だけ呼び出されます。
/// プレイヤーごとに発火する<see cref="Virial.Events.Player.PlayerFixVoteHostEvent"/>より前に呼び出されます。
/// </summary>
public class MeetingFixVoteHostEvent : Event
{
    private MeetingHud meetingHud;
    private Dictionary<byte, int> weightMap;

    internal MeetingFixVoteHostEvent(MeetingHud meetingHud, Dictionary<byte, int> weightMap)
    {
        this.meetingHud = meetingHud;
        this.weightMap = weightMap;
    }

    private PlayerVoteArea? GetVoteArea(Virial.Game.Player voter) => meetingHud.playerStates.FirstOrDefault(state => state.PlayerId.Value == voter.PlayerId);

    /// <summary>
    /// プレイヤーの投票数を取得します。
    /// </summary>
    public int GetVote(Virial.Game.Player voter) => weightMap.TryGetValue(voter.PlayerId, out var vote) ? vote : 1;

    /// <summary>
    /// プレイヤーの投票数を変更します。
    /// </summary>
    public void SetVote(Virial.Game.Player voter, int vote) => weightMap[voter.PlayerId] = vote;

    /// <summary>
    /// プレイヤーが票を投じているかどうか調べます。
    /// </summary>
    public bool GetDidVote(Virial.Game.Player voter)
    {
        var voteArea = GetVoteArea(voter);
        if (voteArea == null) return false;
        var votedFor = voteArea.VotedForId.Value;
        return votedFor != PlayerVoteArea.HasNotVoted && votedFor != PlayerVoteArea.MissedVote && votedFor != PlayerVoteArea.DeadVote;
    }

    /// <summary>
    /// プレイヤーが票を投じているかどうかを変更します。
    /// 票を投じていないプレイヤーに<c>true</c>を指定した場合、スキップ票を投じたものとして扱います。
    /// </summary>
    public void SetDidVote(Virial.Game.Player voter, bool didVote)
    {
        var voteArea = GetVoteArea(voter);
        if (voteArea == null) return;

        if (!didVote)
            voteArea.VotedForId = PlayerVoteArea.MissedVote;
        else if (!GetDidVote(voter))
            voteArea.VotedForId = PlayerVoteArea.SkippedVote;
    }

    /// <summary>
    /// プレイヤーの投票先を取得します。
    /// </summary>
    public bool TryGetVotedFor(Virial.Game.Player voter, out Virial.Game.Player? votedFor)
    {
        votedFor = null;
        if (!GetDidVote(voter)) return false;

        votedFor = NebulaAPI.CurrentGame?.GetPlayer(GetVoteArea(voter)!.VotedForId.Value);
        return true;
    }

    /// <summary>
    /// プレイヤーの投票先を変更します。
    /// 投票先を変更すると、そのプレイヤーは票を投じているものとして扱われます。
    /// </summary>
    public void SetVotedFor(Virial.Game.Player voter, Virial.Game.Player? votedFor)
    {
        var voteArea = GetVoteArea(voter);
        if (voteArea == null) return;

        voteArea.VotedForId = votedFor?.PlayerId ?? PlayerVoteArea.SkippedVote;
    }
}

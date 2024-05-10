using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 投票時に呼び出されます。
/// 自身が操作するプレイヤーに対してのみ呼び出されます。
/// </summary>
public class PlayerVoteCastLocalEvent : AbstractPlayerEvent
{
    public int Vote { get; set; }
    public Virial.Game.Player? VoteFor { get; private init; }
    public Virial.Game.Player Voter => Player;

    internal PlayerVoteCastLocalEvent(Virial.Game.Player voter, Virial.Game.Player? voteFor, int vote) : base(voter)
    {
        this.VoteFor = voteFor;
        this.Vote = vote;
    }
}

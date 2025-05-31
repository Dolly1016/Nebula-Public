using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

/// <summary>
/// 投票時に呼び出されます。
/// </summary>
/// <remarks>
/// ローカルでのみ呼び出されます。
/// </remarks>
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

/// <summary>
/// 投票時に呼び出されます。
/// </summary>
public class PlayerVoteCastEvent : AbstractPlayerEvent
{
    public int Vote { get; private init; }
    public Virial.Game.Player? VoteFor { get; private init; }
    public Virial.Game.Player Voter => Player;

    internal PlayerVoteCastEvent(Virial.Game.Player voter, Virial.Game.Player? voteFor, int vote) : base(voter)
    {
        this.VoteFor = voteFor;
        this.Vote = vote;
    }
}

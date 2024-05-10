﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.Game.Meeting;

public class PlayerVoteDisclosedLocalEvent : AbstractPlayerEvent
{
    public bool VoteToWillBeExiled { get; private init; }
    public Virial.Game.Player? VoteFor { get; private init; }
    public Virial.Game.Player Voter => Player;

    internal PlayerVoteDisclosedLocalEvent(Virial.Game.Player voter, Virial.Game.Player? voteTo, bool voteToWillBeExiled) : base(voter)
    {
        this.VoteFor = voteTo;
        this.VoteToWillBeExiled = voteToWillBeExiled;
    }
}

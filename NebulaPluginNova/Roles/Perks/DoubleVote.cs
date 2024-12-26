using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;

namespace Nebula.Roles.Perks;

internal class DoubleVote : PerkFunctionalInstance
{
    bool used = false;
    static PerkFunctionalDefinition Def = new("doubleVote", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("doubleVote", 2, 32, (Crewmate.Mayor.MyRole as DefinedAssignable).Color), (def, instance) => new DoubleVote(def, instance));


    private DoubleVote(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
    }

    [EventPriority(50)]
    void OnVoted(PlayerVoteCastLocalEvent ev)
    {
        //スキップ、白票、0票投票は対象外
        if (used || ev.VoteFor == null || ev.Vote == 0) return;

        ev.Vote *= 2;
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(!used ? Color.white : Color.gray);
    }
}

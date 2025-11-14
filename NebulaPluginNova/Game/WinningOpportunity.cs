using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Game;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
[NebulaRPCHolder]
internal class WinningOpportunity : AbstractModule<Virial.Game.Game>, IWinningOpportunity, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess)
    {
        DIManager.Instance.RegisterModule(() => new WinningOpportunity());
    }

    private WinningOpportunity()
    {
        ModSingleton<WinningOpportunity>.Instance = this;
        ModSingleton<IWinningOpportunity>.Instance = this;
        this.RegisterPermanently();
    }

    private class Opportunity
    {
        public float Max { get; set { if (field < value) field = value; } } = 0f;

        public float Momentary { get; set; } = 0f;
        public void SetOpportunity(float opportunity, bool isMomentary)
        {
            opportunity = Mathn.Clamp01(opportunity);
            if(isMomentary)
                Momentary = opportunity;
            else
                Max = opportunity;
        }

        public Opportunity() { }

        public void Update(float deltaTime)
        {
            Momentary -= deltaTime;
            if (Momentary < 0f) Momentary = 0f;
        }

        public float CurrentValue => Max > Momentary ? Max : Momentary;
    }
    private Dictionary<RoleTeam, Opportunity> opportunityMap = [];
    float IWinningOpportunity.GetOpportunity(RoleTeam team)
    {
        return opportunityMap.TryGetValue(team, out var opportunity) ? opportunity.CurrentValue : 0f;
    }

    void IWinningOpportunity.SetOpportunity(RoleTeam team, float opportunity, bool isMomentary)
    {
        if (opportunity > 0f)
        {
            if (!opportunityMap.TryGetValue(team, out var o))
            {
                o = new();
                opportunityMap[team] = o;
            }
            o.SetOpportunity(opportunity, isMomentary);
        }
    }

    void IWinningOpportunity.RpcSetOpportunity(RoleTeam team, float opportunity, bool isMomentary)
    {
        if(opportunity > 0f) RpcUpdateOpportunity.Invoke((team.Id, opportunity, isMomentary));
    }

    private void OnUpdate(GameHudUpdateEvent ev)
    {
        var t = Time.deltaTime;
        opportunityMap.Values.Do(o => o.Update(t));
    }

    static private readonly RemoteProcess<(int teamId, float opportunity, bool isMomentary)> RpcUpdateOpportunity = new("UpdateOpportunity", (message, _) =>
    {
        var team = Roles.Roles.GetTeamById(message.teamId);
        if (team == null) return;

        ModSingleton<IWinningOpportunity>.Instance.SetOpportunity(team, message.opportunity, message.isMomentary);
    });
}

using Nebula.Roles.Impostor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class RaiderEvidence : PerkFunctionalInstance, IGameOperator
{
    const float CoolDown = 10f;
    static PerkFunctionalDefinition Def = new("raiderEvidence", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("raiderEvidence", 3, 38, Virial.Color.ImpostorColor, Virial.Color.ImpostorColor).CooldownText("%CD%", () => CoolDown), (def, instance) => new RaiderEvidence(def, instance));

    public RaiderEvidence(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        cooldownTimer = NebulaAPI.Modules.Timer(this, CoolDown);
        cooldownTimer.Start();
        PerkInstance.BindTimer(cooldownTimer);
    }

    private GameTimer cooldownTimer;
    private Raider.RaiderAxe? axe;

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing) return;
        if (!(axe?.CanThrow ?? false)) return;
        if (MyPlayer.IsDead) return;

        NebulaSyncObject.RpcInstantiate(Raider.RaiderAxe.MyGlobalFakeTag, [MyPlayer.PlayerId, axe!.Position.x, axe!.Position.y]);

        NebulaSyncObject.LocalDestroy(axe.ObjectId);
        axe = null;

        cooldownTimer.Start();

        SniperIcon.RegisterAchievementToken(MyPlayer);
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
        if((cooldownTimer.IsProgressing || MyPlayer.IsDead) && axe != null)
        {
            NebulaSyncObject.LocalDestroy(axe.ObjectId);
            axe = null;
        }
        if(!cooldownTimer.IsProgressing && axe == null && MyPlayer.Role.Role != Impostor.Raider.MyRole && !MyPlayer.IsDead)
        {
            axe = (NebulaSyncObject.LocalInstantiate(Raider.RaiderAxe.MyLocalFakeTag, [MyPlayer.PlayerId]).SyncObject as Raider.RaiderAxe)!;
        }
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        cooldownTimer.Start();
    }

    void IGameOperator.OnReleased()
    {
        if(axe != null) NebulaSyncObject.LocalDestroy(axe.ObjectId);
    }
}

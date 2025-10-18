using Nebula.Roles.Impostor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;
using Virial.Events.Game;

namespace Nebula.Roles.Perks;

[NebulaRPCHolder]
internal class FakeBubblegun : PerkFunctionalInstance
{
    const float CoolDown = 10f;
    static PerkFunctionalDefinition Def = new("fakeBubble", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("fakeBubble", 3, 59, Virial.Color.ImpostorColor, Virial.Color.ImpostorColor).CooldownText("%CD%", () => CoolDown), (def, instance) => new FakeBubblegun(def, instance));

    private GameTimer cooldownTimer;
    private FakeBubblegun(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        cooldownTimer = NebulaAPI.Modules.Timer(this, CoolDown);
        cooldownTimer.Start();
        PerkInstance.BindTimer(cooldownTimer);
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing || !MyPlayer.CanMove || MyPlayer.IsDead) return;

        var angle = MyPlayer.Unbox().MouseAngle;
        Vector2 pos = (Vector2)MyPlayer.Position + new Vector2(1f, 0f).Rotate(angle * 180f / Mathn.PI) * (0.7f + 0.1f * Bubblegun.BubbleSize);
        RpcFire.Invoke((MyPlayer, pos, angle));
        cooldownTimer.Start();
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        cooldownTimer.Start();
    }


    static private RemoteProcess<(GamePlayer player, Vector2 pos, float angle)> RpcFire = new("FireFakeBubble",
        (message, _) =>
        {
            Bubblegun.FireFakeBubble(message.player, message.pos, message.angle);
        });
}

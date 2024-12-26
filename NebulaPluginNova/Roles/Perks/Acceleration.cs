using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;

namespace Nebula.Roles.Perks;

internal class Acceleration : PerkFunctionalInstance
{
    const float Cooldown = 30f;
    const float InitialCooldown = 10f;
    const float Duration = 5f;
    const float Rate = 1.5f;
    static PerkFunctionalDefinition Def = new("acceleration", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("acceleration", 3, 0, Virial.Color.CrewmateColor).CooldownText("%CD%", Cooldown).DurationText("%D%", Duration).RateText("%R%", Rate), (def, instance) => new Acceleration(def, instance));

    public Acceleration(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        cooldownTimer = new Timer(Cooldown).Start(InitialCooldown);
        PerkInstance.BindTimer(cooldownTimer);
    }

    private Timer cooldownTimer;

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing) return;

        MyPlayer.GainAttribute(Rate, Duration, false, 0, "perkAccel");
        cooldownTimer.Start();
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        cooldownTimer.Start(InitialCooldown);
    }
}